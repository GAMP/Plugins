using IntegrationLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace BaseLmPlugin
{
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("Registry Import",
        "1.0.0.0",
        "Manages license keys by importing registry file in system registry.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.registry.png")]
    public class RegistyImportPlugin : LicenseManagerBase
    {
        public override void Install(IntegrationLib.IApplicationLicense license, Client.IExecutionContext context, ref bool processCreated)
        {
            //get the license key
            RegistryLicenseKey key = license.KeyAs<RegistryLicenseKey>();

            if (key ==null || string.IsNullOrWhiteSpace(key.Value))
                throw new ArgumentNullException("Invalid key or key value");
            
            //expand and get environment string
            string registryString = Environment.ExpandEnvironmentVariables(key.Value);

            //create registry file
            var regFile = new CoreLib.Registry.CoreRegistryFile();

            //load file from string
            regFile.LoadFromString(registryString);

            //import into registry
            regFile.Import();
        }

        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, System.Windows.Window owner)
        {
            var context = new DialogContext(DialogType.RegistryImport, new RegistryLicenseKey(), profile);
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
            var context = new DialogContext(DialogType.RegistryImport, key, profile);
            if (context.Display(owner))
            {
                return context.Key;
            }
            else
            {
                return null;
            }
        }

        public override bool CanAdd
        {
            get
            {
                return true;
            }
        }

        public override bool CanEdit
        {
            get
            {
                return true;
            }
        }
    }
}
