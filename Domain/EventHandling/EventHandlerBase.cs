// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Its.Domain.EventHandling
{
    public abstract class EventHandlerBase : IEventHandler, INamedEventHandler
    {
        private readonly List<IEventHandler> eventHandlers = new List<IEventHandler>();
        private string name;

        protected virtual void On<T>(Action<T> handle)
        {
            eventHandlers.Add(new DuckTypeProjector<T>(handle));
        }

        public IEnumerable<IEventHandlerBinder> GetBinders()
        {
            return eventHandlers.SelectMany(e => e.GetBinders());
        }

        public virtual string Name
        {
            get
            {
                return name ?? (name = GetType().Name);
            }
            protected set
            {
                name = value;
            }
        }
    }
}
