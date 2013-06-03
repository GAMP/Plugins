using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using IntegrationLib;
using Microsoft.Win32;
using System.IO;
using Client;
using System.Windows;
using SharedLib;
using GizmoShell;
using CoreLib.Diagnostics;
using System.Drawing;
using CoreLib.Imaging;
using System.Diagnostics;

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

                #region Initialize Process
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = uplayPath;
                startInfo.WorkingDirectory = Path.GetDirectoryName(uplayPath);
                startInfo.ErrorDialog = false;
                startInfo.UseShellExecute = false;

                //start origin process
                var uplayProcess = new Process() { StartInfo = startInfo };
                uplayProcess.EnableRaisingEvents = true;
                uplayProcess.Exited += new EventHandler(OnInternalProcessExited);
                #endregion

                #region Start Uplay
                if (uplayProcess.Start())
                {
                    //mark process created
                    forceCreation = true;

                    //atach handlers
                    context.ExecutionStateChaged += new ExecutionContextStateChangedDelegate(OnExecutionStateChaged);

                    //add process to context process list
                    context.AddProcess(uplayProcess, true);

                    if (CoreProcess.WaitForWindowCreated(uplayProcess, 10000, true))
                    {
                        try
                        {
                            //disable input
                            Win32API.Modules.CS.User32.BlockInput(true);

                            //get window
                            WindowInfo uplayWindow = new WindowInfo(uplayProcess.MainWindowHandle);


                            //activate origin window
                            uplayWindow.BringToFront();
                            uplayWindow.Activate();

                            //give some time to activate fields
                            System.Threading.Thread.Sleep(5000);

                            //create input simulator
                            WindowsInput.KeyboardSimulator sim = new WindowsInput.KeyboardSimulator();

                            //clear username filed
                            sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                            //send back to clear any possible typed value
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                            //set username
                            sim.TextEntry(license.KeyAs<UserNamePasswordLicenseKeyBase>().Username);

                            //swicth field
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.TAB);

                            //clear password filed
                            sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                            //send back to clear any possible typed value
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                            //set password
                            sim.TextEntry(license.KeyAs<UserNamePasswordLicenseKeyBase>().Password);

                            //proceed with login
                            sim.KeyDown(WindowsInput.Native.VirtualKeyCode.RETURN);

                            //set environment variable
                            Environment.SetEnvironmentVariable("LICENSEKEYUSER", license.KeyAs<UplayLicenseKey>().Username);

                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            //enable input
                            Win32API.Modules.CS.User32.BlockInput(false);
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

        protected override void OnReportTerminate(IExecutionContext context)
        {
            context.WriteMessage(String.Format("Process {0} caused uplay to exit.", this.TerminateHandle.TerminatingProcesName));
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
