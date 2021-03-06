// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Its.Domain.Serialization;

namespace Microsoft.Its.Domain.Sql.CommandScheduler
{
    public static class SqlCommandSchedulerExtensions
    {
        /// <summary>
        /// Gets a query that will return scheduled commands from the command scheduler database that are due as of the specified time.
        /// </summary>
        /// <param name="query">A queryable for commands from the command scheduler database.</param>
        /// <param name="asOf">The time by which queried commands should be due.</param>
        /// <returns></returns>
        public static IQueryable<ScheduledCommand> Due(this IQueryable<ScheduledCommand> query, DateTimeOffset? asOf = null)
        {
            asOf = asOf ?? Domain.Clock.Now();

            return query.Where(c => c.DueTime <= asOf || c.DueTime == null)
                        .Where(c => c.AppliedTime == null)
                        .Where(c => c.FinalAttemptTime == null)
                        .OrderBy(c => c.DueTime);
        }

        /// <summary>
        /// Deserializes a scheduled command from the database model to the domain model.
        /// </summary>
        internal static CommandScheduled<TAggregate> ToScheduledCommand<TAggregate>(
            this ScheduledCommand scheduled) where TAggregate : IEventSourced
        {
            var json = scheduled.SerializedCommand;
            
            var command = json.FromJsonTo<CommandScheduled<TAggregate>>();
            
            command.Metadata.NumberOfPreviousAttempts = scheduled.Attempts;
            
            return command;
        }
    }
}
