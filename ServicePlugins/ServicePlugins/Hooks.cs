using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IntegrationLib;
using ServerService;
using System.Windows;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.ComponentModel.Composition;

namespace ServicePlugins
{
    #region UserStateHook
    /// <summary>
    /// Simple user state hook.
    /// <remarks>
    /// Can be used to external logging etc.
    /// </remarks>
    /// </summary>
    public class UserStateHook : GizmoServiceHookPluginBase
    {
        public override void OnImportsSatisfied()
        {
        }
    }
    #endregion

    #region HostEventHook
    public class HostEventHook : GizmoServiceHookPluginBase
    {
        public override void OnImportsSatisfied()
        {
        }
    }
    #endregion

    #region ServiceTraceHook
    public class ServiceConsoleTraceHook : GizmoServiceHookPluginBase
    {
        #region FIELDS
        private ConsoleTraceListener listener = new ConsoleTraceListener();
        #endregion

        #region OVERRIDES
        public override void OnImportsSatisfied()
        {
            if (Environment.UserInteractive)
            {
                Trace.Listeners.Add(listener);
            }
        }
        #endregion

        #region CONSOLETRACELISTENER
        /// <summary>
        /// Trace listener implementation that outputs Trace messages to a console window.
        /// </summary>
        public class ConsoleTraceListener : TraceListener
        {
            #region CONSTRUCTOR
            public ConsoleTraceListener()
                : base()
            {
                ConsoleTraceListener.AllocConsole();
                Console.Beep();
                Console.WriteLine(String.Format("{0} {1}", this.GetType().Name, "STARTED CONSOLE OUTPUT"));
            }
            #endregion

            #region NATIVE

            #region AllocConsole
            [SuppressUnmanagedCodeSecurity()]
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool AllocConsole();
            #endregion

            #region FreeConsole
            [SuppressUnmanagedCodeSecurity()]
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool FreeConsole();
            #endregion

            #endregion

            #region OVERRIDES

            public override void Write(string message)
            {
                if (Environment.UserInteractive)
                    Console.Write(message);
            }

            public override void WriteLine(string message)
            {
                if (Environment.UserInteractive)
                    Console.WriteLine(message);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    ConsoleTraceListener.FreeConsole();
            }

            #endregion
        }
        #endregion
    }
    #endregion
}
