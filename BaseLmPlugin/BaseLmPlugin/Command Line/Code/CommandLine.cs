using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using IntegrationLib;
using System.Windows;

namespace BaseLmPlugin
{
    #region CommandLineLicenseManager
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata(
        "Command Line",
        "1.0.0.0",
        "Manages license by launching application executable with license key and executable command line parameters.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.cmd.png")]
    public class CommandLineLicenseManager : LicenseManagerBase
    {
        #region OVERRIDES

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.Executable, key, profile);
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
            var context = new DialogContext(DialogType.Executable, new ProcessLicenseKey(), profile);
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
            if (context.HasCompleted | context.AutoLaunch)
            {
                var licenseKey = license.KeyAs<ProcessLicenseKey>();

                #region Start Process
                //create process for executable
                var process = context.Executable.GetProcessForExecutable(context.Profile);

                //get executable aruments
                string executableArgument = process.StartInfo.Arguments;

                //get expanded key arguments            
                string newArguments = Environment.ExpandEnvironmentVariables(licenseKey.Value);

                if (!string.IsNullOrWhiteSpace(executableArgument))
                {
                    //compile parameters
                    process.StartInfo.Arguments = string.Format("{0} {1}", newArguments, executableArgument);
                }
                else
                {
                    //only license arguments passed
                    process.StartInfo.Arguments = newArguments;
                }

                //start process
                if (process.Start())
                {
                    //executables process creation should not be forced
                    forceCreation = false;

                    //add process to context
                    context.AddProcess(process, true);
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

        #endregion
    } 
    #endregion

    #region Key
    [Serializable()]
    public class CommandLineLicenseKey : ApplicationLicenseKeyBase
    {
    }
    #endregion
}
