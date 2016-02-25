using System;
using System.Linq;
using System.ComponentModel.Composition;
using IntegrationLib;
using Microsoft.Win32;
using System.IO;
using Client;
using System.Windows;
using SharedLib;
using GizmoShell;
using CoreLib.Diagnostics;
using System.Diagnostics;
using Win32API.Modules;

namespace BaseLmPlugin
{
    #region UplayLicenseManager
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("UBI Uplay",
        "1.0.0.0",
        "Manages license keys by sending credentials input to application window.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.uplay.png")]
    public class UplayLicenseManager : SteamLicenseManager
    {
        #region Interface

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, Window owner)
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

        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.UserNamePassword, new UplayLicenseKey(), profile);
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
                #region Validation
                //get installation directory
                string uplayPath = this.GetUplayPath();

                if (String.IsNullOrWhiteSpace(uplayPath) || !File.Exists(uplayPath))
                {
                    context.Client.Log.AddError(String.Format("Uplay client executable not found at {0}.", uplayPath), null, LogCategories.Configuration);
                    return;
                }

                #endregion

                string processName = Path.GetFileNameWithoutExtension(uplayPath); 

                #region Initialize Process

                //get existing uplay process
                var uplayProcess = Process.GetProcessesByName(processName).Where(x => String.Compare(x.MainModule.FileName, uplayPath, true) == 0).FirstOrDefault();

                bool processExisted = uplayProcess != null;

                if(!processExisted)
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = uplayPath;
                    startInfo.WorkingDirectory = Path.GetDirectoryName(uplayPath);
                    startInfo.ErrorDialog = false;
                    startInfo.UseShellExecute = false;

                    //create uplay process
                    uplayProcess = new Process() { StartInfo = startInfo };
                }
           
                uplayProcess.EnableRaisingEvents = true;
                uplayProcess.Exited += new EventHandler(OnInternalProcessExited);

                #endregion

                #region Start Uplay
                if (uplayProcess.Start())
                {
                    //mark process created
                    forceCreation = true;

                    //atach handlers
                    context.ExecutionStateChaged += OnExecutionStateChaged;

                    //add process to context process list
                    context.AddProcess(uplayProcess, true);

                    if (CoreProcess.WaitForWindowCreated(uplayProcess, 120000, true))
                    {
                        try
                        {
                            //disable input
                            User32.BlockInput(true);

                            //get window
                            WindowInfo uplayWindow = new WindowInfo(uplayProcess.MainWindowHandle);

                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //give some time to activate fields
                            System.Threading.Thread.Sleep(5000);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //create input simulator
                            WindowsInput.KeyboardSimulator sim = new WindowsInput.KeyboardSimulator();

                            //send tab
                            sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.TAB);

                            //clear username filed
                            sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //send back to clear any possible typed value
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //set username
                            sim.TextEntry(license.KeyAs<UserNamePasswordLicenseKeyBase>().Username);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //swicth field
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.TAB);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //clear password filed
                            sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //send back to clear any possible typed value
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //set password
                            sim.TextEntry(license.KeyAs<UserNamePasswordLicenseKeyBase>().Password);

                            //disable input
                            User32.BlockInput(true);
                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //proceed with login
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.RETURN);

                            //disable input
                            User32.BlockInput(true);

                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //set environment variable
                            Environment.SetEnvironmentVariable("LICENSEKEYUSER", license.KeyAs<UplayLicenseKey>().Username);

                            //delay installation process
                            System.Threading.Thread.Sleep(5000);
                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            //enable input
                            User32.BlockInput(false);
                        }

                    }
                    else
                    {
                        context.Client.Log.AddError("Uplay client window was not created after specified period of time.", null, LogCategories.Configuration);
                    }
                }
                else
                {
                    context.Client.Log.AddError(String.Format("Uplay client executable {0} could not be started.", uplayPath), null, LogCategories.Configuration);
                }
                #endregion
            }
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
            return new UplayLicenseManagerSettings();
        }

        public override bool DivertExecution(IExecutionContext context)
        {
            return false;
        }

        #endregion

        #region Private

        private string GetUplayPath()
        {
            string modulePath = string.Empty;
            using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Uplay", false))
            {
                if (key != null)
                    modulePath = Path.Combine(key.GetValue("InstallLocation").ToString(), "Uplay.exe");
            }
            return modulePath;
        }

        #endregion
    } 
    #endregion

    #region UplayLicenseManagerSettings
    [Serializable]
    public class UplayLicenseManagerSettings : SteamLicenseManagerSettings, IPluginSettings
    {
    }
    #endregion

    #region UplayLicenseKey
    [Serializable()]
    public class UplayLicenseKey : UserNamePasswordLicenseKeyBase
    {

    }
    #endregion
}
