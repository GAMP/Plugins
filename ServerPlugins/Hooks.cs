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

namespace ServerPlugins
{
    #region UserStateHook
    /// <summary>
    /// Simple user state hook.
    /// <remarks>Can be used to external loging etc.</remarks>
    /// </summary>
    public class UserStateHook : ServiceHookPluginBase
    {
        public override void OnImportsSatisfied()
        {
            this.Service.UserStateChange += new UserStateChangeDelegate(Service_UserStateChange);
            this.Service.UserProfileChange += new UserProfileChangeDelegate(Service_UserProfileChange);
        }

        void Service_UserProfileChange(object sender, UserProfileChangeEventArgs e)
        {
            Trace.WriteLine(e.ToString());
        }

        void Service_UserStateChange(object sender, UserStateEventArgs e)
        {
            Trace.WriteLine(e.ToString());
        }
    } 
    #endregion

    #region HostEventHook
    public class HostEventHook : ServiceHookPluginBase
    {
        public override void OnImportsSatisfied()
        {
            this.Service.HostEvent += new HostEventDelegate(Service_HostEvent);
        }

        void Service_HostEvent(object sender, HostEventArgs e)
        {
            Trace.WriteLine(e.ToString());
        }
    } 
    #endregion

    #region ServiceTraceHook
    public class ServiceTraceHook : ServiceHookPluginBase
    {
        #region Listener
        private ConsoleTraceListener listener = new ConsoleTraceListener();
        #endregion

        public override void OnImportsSatisfied()
        {
            Trace.Listeners.Add(listener);
        }
    } 
    #endregion

    #region ConsoleTraceListener
    public class ConsoleTraceListener : TraceListener
    {
        public ConsoleTraceListener():base()
        {
            ConsoleTraceListener.AllocConsole();
            Console.Beep();
            Console.WriteLine("STARTED CONSOLE OUTPUT");
        }

        public override void Write(string message)
        {            
            Console.Write(message);
        }

        public override void WriteLine(string message)
        {
            Console.WriteLine(message);
        }

        #region Native

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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ConsoleTraceListener.FreeConsole();            
        }

    } 
    #endregion
}
