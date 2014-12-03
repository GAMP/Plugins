using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharedLib.Applications;
using IntegrationLib;
using System.ComponentModel.Composition;
using System.Windows;

namespace BaseLmPlugin
{
    #region InstanceManagementPlugin
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata(
        "Instance",
        "1.0.0.0",
        "Manages license by limiting application istances.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.instance.png")]
    public class InstanceManagementPlugin : LicenseManagerBase
    {
        #region OVERRIDES

        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var totalInstances = profile.Licenses.Count;
            return new InstanceKey() { Value = (totalInstances + 1).ToString() };
        }

        public override bool CanEdit
        {
            get
            {
                return false;
            }
        }

        #endregion
    } 
    #endregion

    #region InstanceKey
    [Serializable()]
    public class InstanceKey : ApplicationLicenseKeyBase
    {
    } 
    #endregion
}
