using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows;
using System.ComponentModel.Composition;
using System.ComponentModel;
using System.IO;
using SharedLib.Applications;
using IntegrationLib;
using SharedLib;

namespace BaseLmPlugin
{
    #region ProcessLicenseManager
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata(
        "Process",
        "1.0.0.0",
        "Manages license keys by passing them to external executables.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.process.png")]
    public class ProcessLicenseManager : ConfigurableLicenseManagerBase
    {
        #region Ovverides

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.Process, key, profile);
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
            var context = new DialogContext(DialogType.Process, new ProcessLicenseKey(), profile);
            if (context.Display(owner))
            {
                return context.Key;
            }
            else
            {
                return null;
            }
        }

        public override void Install(IApplicationLicense license, Client.IExecutionContext context, ref bool forceCreation)
        {
            var settings = this.SettingsAs<ProcessLicenseManagerSettings>();
            var process = settings.GetProcess(license.KeyAs<ProcessLicenseKey>());
            process.Start();
            if (settings.WaitForExit)
            {
                process.WaitForExit();
            }
        }

        public override IPluginSettings GetSettingsInstance()
        {
            return new ProcessLicenseManagerSettings();
        }

        public override UserControl GetConfigurationUI()
        {
            return new ProcessLicenseManagerView();
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

    #region Key
    [Serializable()]
    public class ProcessLicenseKey : ApplicationLicenseKeyBase
    {
    }
    #endregion

    #region Settings
    [Serializable()]
    public class ProcessLicenseManagerSettings : PropertyChangedNotificator,
        IPluginSettings
    {
        #region Fileds
        private string executablePath,
            workingDirectory,
            arguments;
        private bool waitForExit = true, hide = false;
        #endregion

        #region Properties
        public string ExecutablePath
        {
            get { return this.executablePath; }
            set
            {
                this.executablePath = value;
                this.RaisePropertyChanged("ExecutablePath");
            }
        }
        public string Arguments
        {
            get { return this.arguments; }
            set
            {
                this.arguments = value;
                this.RaisePropertyChanged("Arguments");
            }
        }
        public bool WaitForExit
        {
            get { return this.waitForExit; }
            set
            {
                this.waitForExit = value;
                this.RaisePropertyChanged("WaitForExit");
            }
        }
        public string WorkingDirectory
        {
            get { return this.workingDirectory; }
            set
            {
                this.workingDirectory = value;
                this.RaisePropertyChanged("WorkingDirectory");
            }
        }
        public bool Hide
        {
            get { return this.hide; }
            set { this.hide = value; }
        }
        #endregion

        #region Functions
        public Process GetProcess(ProcessLicenseKey key)
        {
            if (key != null)
            {
                var lmProcess = new Process();

                #region Configure Process
                lmProcess.StartInfo.UseShellExecute = false;
                if (this.Hide)
                {
                    lmProcess.StartInfo.CreateNoWindow = true;
                    lmProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                }
                #endregion

                #region Fileds
                string processPath = String.Empty,
                    workingDirectory = String.Empty,
                    arguments = String.Empty,
                    keyValue = String.Empty;
                #endregion

                #region Expand Paths
                if (!String.IsNullOrWhiteSpace(this.ExecutablePath))
                {
                    processPath = Environment.ExpandEnvironmentVariables(this.ExecutablePath);
                }
                else
                {
                    throw new ArgumentException("Executable path cannot be null.");
                }
                if (!String.IsNullOrWhiteSpace(this.WorkingDirectory))
                {
                    workingDirectory = Environment.ExpandEnvironmentVariables(this.WorkingDirectory);
                }
                else
                {
                    workingDirectory = Path.GetDirectoryName(this.ExecutablePath);
                }
                if (!String.IsNullOrWhiteSpace(key.Value))
                {
                    keyValue = Environment.ExpandEnvironmentVariables(key.Value);
                }
                else
                {
                    throw new ArgumentException("Key value cannot be null.");
                }
                if (!String.IsNullOrWhiteSpace(this.Arguments))
                {
                    arguments = Environment.ExpandEnvironmentVariables(this.Arguments);
                    arguments = arguments.Replace("%license%", keyValue);
                }
                #endregion

                #region Assing Values
                lmProcess.StartInfo.EnvironmentVariables.Add("license", keyValue);
                lmProcess.StartInfo.FileName = processPath;
                lmProcess.StartInfo.WorkingDirectory = workingDirectory;
                lmProcess.StartInfo.Arguments = arguments;
                #endregion

                return lmProcess;
            }
            else
            {
                throw new NullReferenceException("License key cannot be null.");
            }
        }
        #endregion
    }
    #endregion
}
