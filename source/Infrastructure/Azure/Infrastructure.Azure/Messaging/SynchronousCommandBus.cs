﻿// ==============================================================================================================
// Microsoft patterns & practices
// CQRS Journey project
// ==============================================================================================================
// ©2012 Microsoft. All rights reserved. Certain content used with permission from contributors
// http://cqrsjourney.github.com/contributors/members
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance 
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is 
// distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and limitations under the License.
// ==============================================================================================================

namespace Infrastructure.Azure.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using Infrastructure.Azure.MessageLog;
    using Infrastructure.Azure.Messaging.Handling;
    using Infrastructure.Messaging;
    using Infrastructure.Messaging.Handling;

    public class SynchronousCommandBus : ICommandBus, ICommandHandlerRegistry
    {
        private readonly ICommandBus commandBus;
        private readonly IAzureMessageLogWriter logWriter;
        private readonly CommandDispatcher commandDispatcher;

        public SynchronousCommandBus(ICommandBus commandBus, IAzureMessageLogWriter logWriter)
        {
            this.commandBus = commandBus;
            this.logWriter = logWriter;
            this.commandDispatcher = new CommandDispatcher();
        }

        public void Register(ICommandHandler commandHandler)
        {
            this.commandDispatcher.Register(commandHandler);
        }

        public void Send(Envelope<ICommand> command)
        {
            if (!this.DoSend(command))
            {
                Trace.TraceInformation("Command with id {0} was not handled locally. Sending it through the bus.", command.Body.Id);
                this.commandBus.Send(command);
            }
        }

        public void Send(IEnumerable<Envelope<ICommand>> commands)
        {
            var handledLocally = true;

            var allCommands = commands.ToList();

            int i = 0;
            for (; i < allCommands.Count && handledLocally; i++)
            {
                handledLocally = this.DoSend(allCommands[i]);
            }

            if (!handledLocally)
            {
                i--;
                Trace.TraceInformation("Command with id {0} was not handled locally. Sending it and all remaining commands through the bus.", allCommands[i].Body.Id);
                this.commandBus.Send(commands.Skip(i));
            }
        }

        private bool DoSend(Envelope<ICommand> command)
        {
            bool handled = false;

            try
            {
                var traceIdentifier = string.Format(CultureInfo.CurrentCulture, " (local handling of command with id {0})", command.Body.Id);
                handled = this.commandDispatcher.ProcessMessage(traceIdentifier, command.Body, command.MessageId, command.CorrelationId);

                //    ThreadPool.QueueUserWorkItem(_ =>
                //    {
                //        var messageLogEntity =
                //            new MessageLogEntity
                //            {
                //            };

                //        this.logWriter.Save(messageLogEntity);
                //    });
            }
            catch (Exception e)
            {
                Trace.TraceWarning("Exception handling command with id {0} synchronously: {1}", command.Body.Id, e.Message);
            }

            return handled;
        }
    }
}
