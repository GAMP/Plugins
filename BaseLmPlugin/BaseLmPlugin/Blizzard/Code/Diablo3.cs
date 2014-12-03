using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IntegrationLib;
using System.ComponentModel.Composition;
using System.Windows;
using System.Drawing;
using System.Diagnostics;
using Win32API.Modules.CS;
using Client;
using CoreLib.Diagnostics;
using GizmoShell;
using SharedLib;

namespace BaseLmPlugin
{
    #region Diablo3LicenseManagerPlugin
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("Diablo III",
        "1.0.0.0",
        "Manages license keys by sending credentials input to application window.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.diablo3.png")]
    public class Diablo3LicenseManagerPlugin : LicenseManagerBase
    {
        #region Constructor
        public Diablo3LicenseManagerPlugin()
        {
            #region Plugin Config
            this.TargetProcessName = "Diablo III";
            this.TargetWindowName = "Diablo III";
            #endregion

            #region PIXEL-MAP
            this.Pixels.Add(ColorTranslator.FromHtml("#7B4D31"));
            this.Pixels.Add(ColorTranslator.FromHtml("#C0904B"));
            this.Pixels.Add(ColorTranslator.FromHtml("#F4EBD2"));
            this.Pixels.Add(ColorTranslator.FromHtml("#FFF6DE"));
            this.Pixels.Add(ColorTranslator.FromHtml("#16160F"));
            #endregion
        }
        #endregion

        #region Fileds
        private int sendWait = 0, windowWait = 500;
        private List<System.Drawing.Color> pixels = new List<System.Drawing.Color>();
        #endregion

        #region Properties

        /// <summary>
        /// Gets or set if waiting should be aborted.
        /// </summary>
        protected bool IsWaitAborted
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets if waiting is currently active.
        /// </summary>
        protected bool IsWaiting
        {
            get;
            set;
        }

        /// <summary>
        /// Gets pixel list.
        /// </summary>
        protected List<System.Drawing.Color> Pixels
        {
            get
            {
                if (this.pixels == null)
                {
                    this.pixels = new List<Color>();
                }
                return this.pixels;
            }
        }

        /// <summary>
        /// Gets or sets the ammount of time to wait after user input has been send to application window.
        /// </summary>
        protected int SendWaitTime
        {
            get { return this.sendWait; }
            set { this.sendWait = value; }
        }

        /// <summary>
        /// Ammount of time to wait for main window.
        /// </summary>
        protected int WindowWaitTime
        {
            get { return this.windowWait; }
            set { this.windowWait = value; }
        }

        /// <summary>
        /// Gets or sets license instance.
        /// </summary>
        private IApplicationLicense License
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets execution context.
        /// </summary>
        private IExecutionContext Context
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets target process name.
        /// </summary>
        protected string TargetProcessName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets target window name.
        /// </summary>
        protected string TargetWindowName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets target process id.
        /// </summary>
        private int TargetProcessId
        {
            get;
            set;
        }

        #endregion

        #region Private Functions

        private void OnExecutionStateChaged(object sender, ExecutionContextStateArgs e)
        {
            switch (e.NewState)
            {
                #region Process Created
                case ContextExecutionState.ProcessCreated:
                    //check if we are already waiting for process
                    if (!this.IsWaiting && e.StateObject is int)
                    {
                        int processId = (int)e.StateObject;
                        try
                        {
                            //get newly created process
                            var createdProcess = Process.GetProcessById(processId);

                            if (String.Compare(createdProcess.ProcessName, this.TargetProcessName, true) == 0)
                            {
                                //set target process id. will help identify exit
                                this.TargetProcessId = processId;

                                //we are now waiting for process window
                                this.IsWaiting = true;

                                //create action
                                var action = new Action<Process>(OnWait);

                                //invoke action
                                action.BeginInvoke(createdProcess, OnWaitCallBack, action);
                            }
                        }
                        catch (ArgumentException)
                        {
                            //process exited
                        }
                    }
                    break;
                #endregion

                #region Process Exited
                case ContextExecutionState.ProcessExited:
                    if (e.StateObject is int && (int)e.StateObject == this.TargetProcessId)
                    {
                        this.OnFinalizePlugin();
                    }
                    break;
                #endregion

                #region Finalization
                case ContextExecutionState.Aborted:
                case ContextExecutionState.Destroyed:
                case ContextExecutionState.Released:
                case ContextExecutionState.Failed:
                case ContextExecutionState.Finalized:
                    this.OnFinalizePlugin();
                    break;
                default: break;
                #endregion
            }
        }

        protected void OnWait(Process process)
        {
            try
            {
                //block user input
                User32.BlockInput(true);

                while (!this.IsWaitAborted)
                {
                    //get process windows list
                    var windowList = WindowEnumerator.ListAllWindows(process.Id);

                    //find desired window
                    IntPtr desiredWindow = (from win in windowList where String.Compare(win.Title, this.TargetWindowName, true) == 0 select win.Handle).FirstOrDefault();

                    //if desired window found start checking its pixels
                    if (desiredWindow != IntPtr.Zero)
                    {

                        //get app window snapshot
                        Image applicationImage = CoreLib.Imaging.Imaging.CaptureWindowImage(desiredWindow);

                        //ammount of matched pixels
                        int pixelsMatched = 0;

                        //create new pixels-map
                        var pixelList = this.Pixels.ToList<Color>();

                        //match pixels
                        if (CoreLib.Imaging.Imaging.HasMatchingPixels(applicationImage, pixelList, out pixelsMatched))
                        {
                            UserNamePasswordLicenseKeyBase key = this.License.KeyAs<UserNamePasswordLicenseKeyBase>();

                            //unblock input
                            User32.BlockInput(false);

                            //send input to the application
                            this.OnSendInput(desiredWindow, key.Username, key.Password);

                            //exit loop
                            break;
                        }
                    }

                    if (this.WindowWaitTime > 0)
                    {
                        //wait 
                        System.Threading.Thread.Sleep(this.WindowWaitTime);
                    }
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                //always unblock input
                User32.BlockInput(false);
            }
        }

        protected virtual void OnSendInput(IntPtr hwnd, string username, string password)
        {
            #region Validation
            if (hwnd == IntPtr.Zero)
            {
                throw new ArgumentException("Hwnd", "Invalid window handle specified.");
            }
            if (String.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentNullException("Username", "Invalid username specified.");
            }
            if (String.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentNullException("Password", "Invalid password specified.");
            }
            #endregion

            //get window info
            WindowInfo window = new WindowInfo(hwnd);

            //check if window is valid
            if (window.IsValidWindow)
            {

                try
                {
                    //block user input
                    User32.BlockInput(true);

                    //restore window (case it minimized)
                    window.Restore(false);

                    //bring window to front
                    window.BringToFront();

                    //activate window
                    window.Activate();

                    //create input simulator
                    WindowsInput.KeyboardSimulator sim = new WindowsInput.KeyboardSimulator();

                    //clear username filed
                    sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                    //send back to clear any possible typed value
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                    //send username
                    sim.TextEntry(username);

                    //change input field
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.TAB);

                    //clear password filed
                    sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                    //send back to clear any possible typed value
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                    //send bassword
                    sim.TextEntry(password);

                    //initiate login
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.RETURN);

                    if (this.SendWaitTime > 0)
                    {
                        //sleep few seconds to allow login
                        System.Threading.Thread.Sleep(this.SendWaitTime);
                    }
                }
                catch
                {
                    //ignore
                }
                finally
                {

                    //unblock input
                    User32.BlockInput(false);
                }
            }
        }

        protected void OnWaitCallBack(IAsyncResult result)
        {
            try
            {
                var invoker = (Action<Process>)result.AsyncState;
                invoker.EndInvoke(result);
            }
            catch
            {
                //ignore errors here
            }
            finally
            {
                //finalize plugin
                this.OnFinalizePlugin();
            }
        }

        protected virtual UserNamePasswordLicenseKeyBase GetKeyInstance()
        {
            return new Diablo3Key();
        }

        protected void OnFinalizePlugin()
        {
            if (this.Context != null)
            {
                //remove handlers    
                this.Context.ExecutionStateChaged -= OnExecutionStateChaged;
            }

            //stop waiting in any case
            this.IsWaitAborted = true;

            //no longer waiting
            this.IsWaiting = false;
        }

        #endregion

        #region OVERRIDES

        public override void Install(IApplicationLicense license, IExecutionContext context, ref bool forceCreation)
        {
            //clear current account
            using(var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Blizzard Entertainment\Battle.net\D3",true))
            {
                if(key !=null )
                    key.DeleteValue("AccountName",false);               
            }

            this.License = license;
            this.Context = context;
            context.ExecutionStateChaged += OnExecutionStateChaged;

            //set environment variable
            Environment.SetEnvironmentVariable("LICENSEKEYUSER", license.KeyAs<UserNamePasswordLicenseKeyBase>().Username);
        }

        public override void Uninstall(IApplicationLicense license)
        {
            //reset environment variable
            Environment.SetEnvironmentVariable("LICENSEKEYUSER", String.Empty);
        }

        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, System.Windows.Window owner)
        {
            var context = new DialogContext(DialogType.UserNamePassword, this.GetKeyInstance(), profile);
            if (context.Display(owner))
            {
                return context.Key;
            }
            else
            {
                return null;
            }
        }

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, System.Windows.Window owner)
        {
            var context = new DialogContext(DialogType.UserNamePassword, key, profile);
            if (context.Display(owner))
            {
                return context.Key;
            }
            else
            {
                return null;
            }
        }

        public override bool CanEdit
        {
            get
            {
                return true;
            }
        }

        #endregion
    }
    #endregion

    #region Diablo3Key
    [Serializable()]
    public class Diablo3Key : UserNamePasswordLicenseKeyBase
    {
    }
    #endregion
}
