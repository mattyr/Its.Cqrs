﻿using System;
using System.Diagnostics;

namespace Microsoft.Its.Domain
{
    [DebuggerStepThrough]
    public abstract class ScheduledCommandResult
    {
        private readonly IScheduledCommand command;

        protected ScheduledCommandResult(IScheduledCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }
            this.command = command;
        }

        public virtual IScheduledCommand ScheduledCommand
        {
            get
            {
                return command;
            }
        }

        public abstract bool WasSuccessful { get; }
    }
}