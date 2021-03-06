// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Its.Domain
{
    /// <summary>
    /// Specifies the name used to store an event type in the event store.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class EventNameAttribute : Attribute
    {
        public EventNameAttribute(string eventName)
        {
            EventName = eventName;
        }

        /// <summary>
        /// Gets or sets the name used to store the event in the event store.
        /// </summary>
        public string EventName { get; set; }
    }
}
