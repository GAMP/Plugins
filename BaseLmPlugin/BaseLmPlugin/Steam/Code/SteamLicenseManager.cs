using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IntegrationLib;
using System.ComponentModel.Composition;
using SharedLib;
using BaseLmPlugin;
using System.Windows;
using Client;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Threading;

namespace BaseLmPlugin
{
    #region SteamLicenseManager
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata(
        "Steam",
        "1.0.0.0",
        "Manages license keys by launching Steam with login parameters.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.steam.png")]
    public class SteamLicenseManager : ConfigurableLicenseManagerBase
    {
        #region Fileds
        private TerminateWaitHandle terminateWaitHandle = new TerminateWaitHandle(false);
        #endregion

        #region Properties
        protected TerminateWaitHandle TerminateHandle
        {
            get { return this.terminateWaitHandle; }
        }
        #endregion

        #region Ovverides

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.Steam, key, profile);
            if (context.Display(owner))
            {
                return context.Key;
            }
            else
            {
                return null;
            }
        }

        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.Steam, new SteamLicenseKey(), profile);
            if (context.Display(owner))
            {
                return context.Key;
            }
            else
            {
                return null;
            }
        }

        public override void Install(IApplicationLicense license, IExecutionContext context, ref bool forceCreation)
        {
            if (context.HasCompleted | context.AutoLaunch)
            {
                #region Variables
                string executablePath = String.Empty,
                    workingDirectory = String.Empty,
                    arguments = String.Empty;
                var key = license.KeyAs<SteamLicenseKey>();
                #endregion

                #region Initialize Variables
                if (!String.IsNullOrWhiteSpace(context.Executable.ExecutablePath))
                {
                    executablePath = Environment.ExpandEnvironmentVariables(context.Executable.ExecutablePath);
                }
                else
                {
                    throw new ArgumentNullException("Steam executable path invalid", "ExecutablePath");
                }
                if (!String.IsNullOrWhiteSpace(context.Executable.WorkingDirectory))
                {
                    workingDirectory = Environment.ExpandEnvironmentVariables(context.Executable.WorkingDirectory);
                }
                else
                {
                    workingDirectory = Path.GetDirectoryName(executablePath);
                }
                if (!String.IsNullOrWhiteSpace(context.Executable.Arguments))
                {
                    arguments = Environment.ExpandEnvironmentVariables(context.Executable.Arguments);
                }
                arguments = String.Format("-login {0} {1} {2}", key.Username, key.Password, arguments);
                #endregion

                #region Initialize Process
                var streamProcess = new Process();
                streamProcess.StartInfo.FileName = executablePath;
                streamProcess.StartInfo.WorkingDirectory = workingDirectory;
                streamProcess.StartInfo.Arguments = arguments;
                streamProcess.StartInfo.UseShellExecute = false;
                streamProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                streamProcess.EnableRaisingEvents = true;
                streamProcess.Exited += new EventHandler(OnInternalProcessExited);
                #endregion

                #region Start steam process
                context.ExecutionStateChaged += new ExecutionContextStateChangedDelegate(OnExecutionStateChaged);
                if (streamProcess.Start())
                {
                    //executables process creation should not be forced
                    forceCreation = false;

                    //add process to context
                    context.AddProcess(streamProcess, true);

                    //set environment variables
                    if (!String.IsNullOrWhiteSpace(key.Username))
                        Environment.SetEnvironmentVariable("LICENSEKEYUSER", key.Username);

                    if (!String.IsNullOrWhiteSpace(key.AccountId))
                        Environment.SetEnvironmentVariable("LICENSEKEYUSERID", key.AccountId);
                }
                else
                {
                    throw new Exception("Steam process was not created.");
                }
                #endregion
            }       
        }

        public override void Uninstall(IApplicationLicense license)
        {
            //reset variable
            Environment.SetEnvironmentVariable("LICENSEKEYUSER", String.Empty);
            Environment.SetEnvironmentVariable("LICENSEKEYUSERID", String.Empty);
        }

        public override bool CanEdit
        {
            get
            {
                return true;
            }
        }

        public override System.Windows.Controls.UserControl GetConfigurationUI()
        {
            return new SteamSettingsView();
        }

        public override IPluginSettings GetSettingsInstance()
        {
            return new SteamLicenseManagerSettings();
        }

        #endregion

        #region Virtual

        protected virtual void OnInternalProcessExited(object sender, EventArgs e)
        {

        }

        protected virtual void OnExecutionStateChaged(object sender, ExecutionContextStateArgs e)
        {
            #region Local Variables
            var settings = this.SettingsAs<SteamLicenseManagerSettings>();
            IExecutionContext context = (IExecutionContext)sender; 
            #endregion

            #region Child Exit
            if (e.NewState == ContextExecutionState.ProcessExited)
            {
                if (settings.TerminateOnChildExit)
                {
                    //get the exited process
                    int exitedProcessId = (int)e.StateObject;

                    //check if the child is default browser
                    string processFileName = string.Empty;

                    //get the file name of exited process
                    context.TryGetProcessFileName(exitedProcessId, out processFileName);

                    if (!String.IsNullOrWhiteSpace(processFileName))
                    {
                        if (settings.IsMatch(processFileName))
                        {
                            lock (this.TerminateHandle)
                            {
                                //start waiting termination
                                if (!this.TerminateHandle.WaitingTermination)
                                {
                                    //set waiting flag
                                    this.TerminateHandle.WaitingTermination = true;
                                    
                                    //assign triggered proces file name
                                    this.TerminateHandle.TerminatingProcesName = processFileName;
                                    
                                    //reset wait handle
                                    this.TerminateHandle.Reset();

                                    //async begin wating
                                    Action<IExecutionContext> del = new Action<IExecutionContext>(WaitForTerminateWorker);
                                    del.BeginInvoke(context, WaitForTerminateCallback, del);
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Child Created
            else if (e.NewState == ContextExecutionState.ProcessCreated)
            {
                if (settings.TerminateOnChildExit)
                {
                    //get the exited process
                    int startedProcessId = (int)e.StateObject;

                    //create process file name
                    string processFileName = string.Empty;

                    //get process name or process file name
                    context.TryGetProcessFileName(startedProcessId, out processFileName);


                    if (!String.IsNullOrWhiteSpace(processFileName))
                    {
                        //validate process name
                        if (settings.IsMatch(processFileName))
                        {
                            lock (this.TerminateHandle)
                            {
                                //reset waiting
                                if (this.TerminateHandle.WaitingTermination)
                                {
                                    this.TerminateHandle.WaitingTermination = false;
                                    this.TerminateHandle.Set();
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Finished
            else if (e.NewState == ContextExecutionState.Finalized | e.NewState == ContextExecutionState.Destroyed | e.NewState == ContextExecutionState.Released)
            {
                context.ExecutionStateChaged -= new ExecutionContextStateChangedDelegate(OnExecutionStateChaged);
            }
            #endregion
        }

        protected virtual void WaitForTerminateWorker(IExecutionContext context)
        {
            //get ammount to wait from settings
            int waitMiliseconds = (int)TimeSpan.FromSeconds(this.SettingsAs<SteamLicenseManagerSettings>().ChildWaitTimeout).TotalMilliseconds;
            
            //start waiting
            this.TerminateHandle.Wait(waitMiliseconds);
            
            //check if waiting was reset
            if (this.TerminateHandle.WaitingTermination)
            {
                //kill all processes in context
                context.Kill();

                //report terminate triggered process
                this.OnReportTerminate(context);
            }
        }

        protected virtual void OnReportTerminate(IExecutionContext context)
        {
            context.WriteMessage(String.Format("Process {0} caused steam to exit.", this.TerminateHandle.TerminatingProcesName));
        }

        protected virtual void WaitForTerminateCallback(IAsyncResult result)
        {
            try
            {
                Action<IExecutionContext> del = (Action<IExecutionContext>)result.AsyncState;
                del.EndInvoke(result);
            }
            catch
            {

            }
        }

        #endregion
    }
    #endregion

    #region SteamLicenseManagerSettings
    [Serializable]
    public class SteamLicenseManagerSettings : PropertyChangedNotificator, IPluginSettings
    {
        #region Fileds
        private bool terminate = true;
        private string childIgnoreList = String.Empty;
        private int childWaitTimeout = 5;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets if context should be terminated once a child exists.
        /// </summary>
        public bool TerminateOnChildExit
        {
            get { return this.terminate; }
            set
            {
                this.terminate = value;
                this.RaisePropertyChanged("TerminateOnChildExit");
            }
        }
        /// <summary>
        /// List of processes seperated by that should trigger context termination once exited.
        /// <remarks>Full name or process name can be specified. Process names should be seperated by ; mark.</remarks>
        /// </summary>
        public string ChildIgnoreList
        {
            get { return this.childIgnoreList; }
            set
            {
                this.childIgnoreList = value;
                this.RaisePropertyChanged("ChildIgnoreList");
            }
        }
        /// <summary>
        /// Ammount of time to wait before terminating context.
        /// </summary>
        public int ChildWaitTimeout
        {
            get { return this.childWaitTimeout; }
            set
            {
                this.childWaitTimeout = value;
                this.RaisePropertyChanged("ChildWaitTimeout");
            }
        }
        #endregion

        #region Functions
        public bool IsMatch(string fileName)
        {
            if (!String.IsNullOrWhiteSpace(this.ChildIgnoreList))
            {
                string processName = Path.GetFileName(fileName);
                string processNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                string processNameWithExtension = Path.GetFileNameWithoutExtension(fileName) + ".exe";
                foreach (string childName in this.ChildIgnoreList.Split(';'))
                {
                    if (!String.IsNullOrWhiteSpace(childName))
                    {
                        if (processName.ToLower() == childName.ToLower() ||
                            processNameWithoutExtension.ToLower() == childName.ToLower() ||
                            processNameWithExtension.ToLower() == childName.ToLower())
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        #endregion
    }
    #endregion

    #region Steam Key
    [Serializable()]
    public class SteamLicenseKey : ApplicationLicenseKeyBase
    {
        #region Fields
        private string
            username,
            password,
            accountId;
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets licenses username.
        /// </summary>
        public string Username
        {
            get { return this.username; }
            set
            {
                this.username = value;
                this.RaisePropertyChanged("Username");
            }
        }

        /// <summary>
        /// Gets or sets license password.
        /// </summary>
        public string Password
        {
            get { return this.password; }
            set
            {
                this.password = value;
                this.RaisePropertyChanged("Password");
            }
        }

        /// <summary>
        /// Gets or sets account id.
        /// </summary>
        public string AccountId
        {
            get { return this.accountId; }
            set
            {
                this.accountId = value;
                this.RaisePropertyChanged("AccountId");
            }
        }

        /// <summary>
        /// Gets if license is valid.
        /// </summary>
        public override bool IsValid
        {
            get
            {
                return !((String.IsNullOrWhiteSpace(this.Username) & (String.IsNullOrWhiteSpace(this.Password))));
            }
        }

        /// <summary>
        /// Gets license literal string representation.
        /// </summary>
        public override string KeyString
        {
            get
            {
                return String.Format("Username:{0} | Password:{1}",
                    !String.IsNullOrWhiteSpace(this.Username) ? this.Username : "Invalid Username",
                    !String.IsNullOrWhiteSpace(this.Password) ? this.Password : "Invalid Password");
            }
        }

        #endregion

        #region Ovveride
        public override string ToString()
        {
            return this.KeyString;
        }
        #endregion
    }
    #endregion
}
