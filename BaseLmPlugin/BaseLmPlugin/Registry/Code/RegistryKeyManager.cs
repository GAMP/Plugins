using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Data;
using SharedLib.Applications;
using IntegrationLib;
using SharedLib;
using System.ComponentModel.Composition;
using System.Windows;
using System.IO;

namespace BaseLmPlugin
{
    #region RegistryLicenseManager
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata(
        "Registry",
        "1.0.0.0",
        "Manages license keys by setting key values in system registry.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.registry.png")]
    public class RegistryLicenseManager : ConfigurableLicenseManagerBase
    {
        #region Ovverides
        
        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.Registry, new RegistryLicenseKey(), profile);
            if (context.Display(owner))
            {
                return context.Key;
            }
            else
            {
                return null;
            }
        }

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.Registry, key, profile);
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
            #region Initialize Variables
            var settings = this.SettingsAs<RegistryLicenseManagerSettings>();
            string registryPath = string.Empty,valuePath = string.Empty;
            object value = null;
            if (!String.IsNullOrWhiteSpace(settings.RegistryPath))
            {
                registryPath = Environment.ExpandEnvironmentVariables(settings.KeyPath);
                valuePath = Environment.ExpandEnvironmentVariables(settings.ValueName);
                if (settings.ValueKind == RegistryValueKind.Binary)
                {
                    #region Convert string to binary
                    try
                    {
                        string[] bytes = license.KeyAs<RegistryLicenseKey>().Value.Split(',');
                        byte[] array = new byte[bytes.Count()];
                        int index = 0;
                        foreach (var stringByte in bytes)
                        {
                            array[index] = Byte.Parse(stringByte, System.Globalization.NumberStyles.HexNumber);
                            index++;
                        }
                        value = array;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Could not convert binary data.", ex);
                    }
                    #endregion
                }
                else
                {
                    value = Environment.ExpandEnvironmentVariables(license.KeyAs<RegistryLicenseKey>().Value);
                }
            }
            else
            {
                throw new Exception("Invalid registry path specified.");
            }
            #endregion
            
            #region Get base key
            RegistryKey baseKey = null;
            switch (settings.Hive)
            {
                case RegistryHive.Users:
                    baseKey = Registry.Users;
                    break;
                case RegistryHive.PerformanceData:
                    baseKey = Registry.PerformanceData;
                    break;
                case RegistryHive.ClassesRoot:
                    baseKey = Registry.ClassesRoot;
                    break;
                case RegistryHive.CurrentConfig:
                    baseKey = Registry.CurrentConfig;
                    break;
                case RegistryHive.CurrentUser:
                    baseKey = Registry.CurrentUser;
                    break;
                case RegistryHive.LocalMachine:
                    baseKey = Registry.LocalMachine;
                    break;
                default:
                    throw new ArgumentException("Invalid registry hive specified.");
            }
            #endregion

            #region Set Values
            var destinationKey = baseKey.OpenSubKey(registryPath, true);
            if (destinationKey == null)
            {
                destinationKey = baseKey.CreateSubKey(registryPath);
            }
            destinationKey.SetValue(valuePath, value, settings.ValueKind);
            destinationKey.Close();
            #endregion
        }

        #endregion

        #region IConfigurable
        public override UserControl GetConfigurationUI()
        {
            return new RegistrySettingsView();
        }
        public override IPluginSettings GetSettingsInstance()
        {
            return new RegistryLicenseManagerSettings();
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
    public class RegistryLicenseKey : ApplicationLicenseKeyBase
    {
        public override bool IsValid
        {
            get
            {
                return !String.IsNullOrWhiteSpace(this.Value);
            }
        }

        public override string KeyString
        {
            get
            {
                return String.IsNullOrWhiteSpace(this.Value) ? null : this.Value.Split(System.Environment.NewLine.ToCharArray()).FirstOrDefault() ;;
            }
        }
    }
    #endregion
    
    #region Settings
    [Serializable()]
    public class RegistryLicenseManagerSettings : PropertyChangedNotificator, IPluginSettings
    {
        #region Fileds
        private string registryPath;
        private Microsoft.Win32.RegistryHive registryHive = RegistryHive.CurrentUser;
        RegistryValueKind valueKind = RegistryValueKind.String;
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets hive of this registry key.
        /// </summary>
        public RegistryHive Hive
        {
            get { return this.registryHive; }
            set
            {
                this.registryHive = value;
                this.RaisePropertyChanged("Hive");
            }
        }
        /// <summary>
        /// Gets or sets path to the registry key.
        /// </summary>
        public string RegistryPath
        {
            get { return this.registryPath; }
            set
            {
                this.registryPath = value;
                this.RaisePropertyChanged("RegistryPath");
            }
        }
        /// <summary>
        /// Gets or sets the value kind of registry key.
        /// </summary>
        public RegistryValueKind ValueKind
        {
            get { return this.valueKind; }
            set
            {
                this.valueKind = value;
                this.RaisePropertyChanged("ValueKind");
            }
        }
        /// <summary>
        /// Gets normalized registry path.
        /// </summary>
        public string KeyPath
        {
            get
            {             
                if (!String.IsNullOrWhiteSpace(this.RegistryPath))
                {
                    string normalizedPath = this.RegistryPath;
                    if (normalizedPath.StartsWith(@"\"))
                    {
                        normalizedPath = normalizedPath.Remove(0, 1);
                    }
                    if (!normalizedPath.EndsWith(@"\"))
                    {
                        return Path.GetDirectoryName(normalizedPath);
                    }
                    else
                    {
                        return Environment.ExpandEnvironmentVariables(normalizedPath);
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        /// <summary>
        /// Gets the value name.
        /// <remarks>Returns empty string if no value name is present in the registry path.</remarks>
        /// </summary>
        public string ValueName
        {
            get
            {
                if (!String.IsNullOrWhiteSpace(this.RegistryPath))
                {
                    if (!this.RegistryPath.EndsWith(@"\"))
                    {
                        return Path.GetFileName(this.RegistryPath);
                    }
                    else
                    {
                        return String.Empty;
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
        }
        #endregion
    }
    #endregion
}
