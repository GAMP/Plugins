﻿using Client;
using GizmoShell;
using IntegrationLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Win32API.Modules;
using WindowsInput;

namespace BaseLmPlugin
{
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("Battle.NET",
        "1.0.0.0",
        "Manages license keys by sending credentials input to application window.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.battlenet.png")]
    public class BattleNetLicenseManager : SteamLicenseManager
    {
        #region Fields

        private string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net", "Battle.net.config");
        private string[] processImageFileNames = new string[] 
        {
            @"Battle.net Launcher",
            @"Battle.net",
            @"BlizzardError",
            @"Agent",
            @"Battle.net Helper",
            @"SystemSurvey"
        };

        CancellationTokenSource cToken;

        #endregion

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.UserNamePassword, key, profile);
            return context.Display(owner) ? context.Key : null;
        }

        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.UserNamePassword, new BattleNetLicenseKey(), profile);
            return context.Display(owner) ? context.Key : null;
        }

        public override void Install(IApplicationLicense license, IExecutionContext context, ref bool forceCreation)
        {           
            if (context.HasCompleted | context.AutoLaunch)
            {
                if(cToken!=null && cToken.Token.CanBeCanceled)
                {
                    cToken.Cancel();
                }
                else
                {
                    cToken = new CancellationTokenSource();
                }

                var settings = this.SettingsAs<BattleNetManagerSettings>();
                var key = license.KeyAs<BattleNetLicenseKey>();
                
                #region VALIDATION

                if (settings == null)
                    throw new ArgumentNullException("Settings");

                if (key == null)
                    throw new ArgumentNullException("Key");

                if (string.IsNullOrWhiteSpace(key.Username))
                    throw new ArgumentNullException("Username");

                if (string.IsNullOrWhiteSpace(key.Password))
                    throw new ArgumentNullException("Password");
                
                #endregion

                //get full path to executable
                string fulleExePath = Environment.ExpandEnvironmentVariables(context.Executable.ExecutablePath);

                //if working directory not specified set it to null
                string workingDirectory = string.IsNullOrWhiteSpace(context.Executable.WorkingDirectory) ? null : Environment.ExpandEnvironmentVariables(context.Executable.WorkingDirectory);

                string userName = key.Username;
                string passWord = key.Password;

                #region VALIDATE FILE EXISTS
                if (!File.Exists(fulleExePath))
                {
                    context.WriteMessage(string.Format("BattleNet executable not found at {0}", fulleExePath));
                    return;
                }
                #endregion

                #region PROCESS CLEANUP
                //ensure no battlenet processes running
                foreach (var processModuleFileName in processImageFileNames)
                {
                    if (string.IsNullOrWhiteSpace(processModuleFileName))
                        continue;

                    var processList = Process.GetProcessesByName(processModuleFileName);
                    processList.ToList().ForEach(x =>
                    {
                        try
                        {
                            x.Kill();
                        }
                        catch
                        {
                            Trace.WriteLine(string.Format("Could not kill BattleNet process {0}", processModuleFileName));
                        }
                    });
                }
                #endregion

                #region CONFIG CLEANUP
                //ensure configuration valid for login operation
                if (File.Exists(configPath))
                {
                    var fullConfig = File.ReadAllText(configPath);
                    fullConfig = RemoveLineForPattern(fullConfig, @"AutoLogin");
                    fullConfig = RemoveLineForPattern(fullConfig, @"SavedAccountNames");
                    File.WriteAllText(configPath, fullConfig);
                }
                #endregion

                //create process start info
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = fulleExePath;
                startInfo.WorkingDirectory = workingDirectory;

                //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                //startInfo.CreateNoWindow = true;

                //create process class
                Process bnProcess = new Process() { StartInfo = startInfo };

                //try to start process and add it to execution context
                if (context.AddProcessIfStarted(bnProcess,true))
                {
                    //assign event handler
                    context.ExecutionStateChaged += OnExecutionStateChaged; 

                    //enable event raising and hook event handlers
                    bnProcess.EnableRaisingEvents = true;
                    bnProcess.Exited += OnInternalProcessExited;                                    

                    //wait for battlenet window
                    var task = WindowInfo.WaitForWindowCreatedAsync("Battle.net Login", -1,cToken.Token );

                    try
                    {
                        task.Wait();
                    }
                    catch (AggregateException aex)
                    {
                        aex.Handle(ex =>
                        {
                            // Handle the cancelled tasks
                            TaskCanceledException tcex = ex as TaskCanceledException;
                            if (tcex != null)
                            {
                                Console.WriteLine("Handling cancellation of task {0}", tcex.Task.Id);
                                return true;
                            }

                            // Not handling any other types of exception.
                            return false;
                        });
                        return;
                    }

                    if (!task.Result.Item1)
                    {
                        //window not found
                        return;
                    }

                    Process targetProcess = task.Result.Item2.First();

                    #region SIM
                    try
                    {

                        IntPtr window = targetProcess.MainWindowHandle;

                        WindowInfo info = new WindowInfo(window);

                        if (info.IsMinimized)
                            info.Restore();

                        info.BringToFront();
                        info.Activate();

                        //block user input
                        User32.BlockInput(true);

                        Thread.Sleep(5000);

                        KeyboardSimulator sim = new KeyboardSimulator();
                        MouseSimulator msim = new MouseSimulator();

                        var x = info.Location.X + info.Width - 150;
                        var y = info.Location.Y + 150;
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
                        msim.LeftButtonClick();

                        //clear username filed
                        sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                        //send back to clear any possible typed value
                        sim.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);

                        //set username
                        sim.TextEntry(userName);

                        //reactivate
                        info.BringToFront();
                        info.Activate();

                        x = info.Location.X + info.Width - 150;
                        y = info.Location.Y + 200;
                        System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);

                        msim.LeftButtonClick();
    
                        //clear password filed
                        sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                        //send back to clear any possible typed value
                        sim.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);

                        //set password
                        sim.TextEntry(passWord);

                        //proceed with login
                        sim.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);
                    }
                    catch(Exception ex)
                    {
                        context.WriteMessage(string.Format("License installation failed {0}", ex.Message));
                    }  
                    finally
                    {
                        //unblock user input
                        User32.BlockInput(false);
                    }
                    #endregion
                }
            }
        }

        protected override void OnExecutionStateChaged(object sender, ExecutionContextStateArgs e)
        {
            base.OnExecutionStateChaged(sender, e);
            if(e.NewState == SharedLib.ContextExecutionState.Aborting)
            {
                if(cToken!=null && cToken.Token.CanBeCanceled)
                {
                    cToken.Cancel();
                }
            }
        }

        public override void Uninstall(IApplicationLicense license)
        {
        }

        public override bool CanEdit
        {
            get
            {
                return true;
            }
        }

        public override UserControl GetConfigurationUI()
        {
            return new SteamSettingsView();
        }

        public override IPluginSettings GetSettingsInstance()
        {
            return new BattleNetManagerSettings();
        }

        private string RemoveLineForPattern(string stringValue, string pattern)
        {
            var index = stringValue.IndexOf(pattern);

            if (index != -1)
            {
                //get previous new line
                int nlStartIndex = stringValue.LastIndexOf(Environment.NewLine, index);
                if (nlStartIndex == -1)
                {
                    //start from the beggining
                    nlStartIndex = 0;
                }

                int nlEndIndex = stringValue.IndexOf(Environment.NewLine, index);
                if (nlEndIndex == -1)
                {
                    //go up to the end of the string
                    nlEndIndex = stringValue.Length;
                }

                int length = nlEndIndex - nlStartIndex;

                stringValue = stringValue.Remove(nlStartIndex, length);

            }

            return stringValue;
        }
    }

    #region BattleNetManagerSettings
    [Serializable]
    public class BattleNetManagerSettings : SteamLicenseManagerSettings, IPluginSettings
    {
    }
    #endregion

    #region BattlenetLicenseKey
    [Serializable()]
    public class BattleNetLicenseKey : UserNamePasswordLicenseKeyBase
    {
    }
    #endregion
}
