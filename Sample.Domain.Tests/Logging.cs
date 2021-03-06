// Copyright (c) Microsoft. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.Its.Log.Instrumentation;
using TraceListener = Microsoft.Its.Log.Instrumentation.TraceListener;

namespace Sample.Domain.Tests
{
    public static class Logging
    {
        private static readonly TraceListener itsLogListener = new TraceListener();

        public static void Configure()
        {
            if (!Trace.Listeners.Contains(itsLogListener))
            {
                Trace.Listeners.Add(itsLogListener);

                Log.EntryPosted += (o, e) => Console.WriteLine(e.LogEntry.ToLogString());
            }
        }
    }
}
