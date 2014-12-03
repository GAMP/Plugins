using Client;
using IntegrationLib;
using Newtonsoft.Json;
using SharedLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;

namespace BaseLmPlugin
{
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("Minecraft",
        "1.0.0.0",
        "Manages license keys by writing auth token to application configuration.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.minecraft.png")]
    public class MineCraftLicenseManager : LicenseManagerBase
    {
        #region FIELDS
        private static string api_path = @"https://authserver.mojang.com/authenticate";
        private static string target_directory_path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        private static string target_file_path = System.IO.Path.Combine(target_directory_path, "launcher_profiles.json");
        #endregion

        #region OVERRIDES

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

        public override IApplicationLicenseKey EditLicense(IApplicationLicenseKey key, ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.UserNamePassword, key, profile);
            return context.Display(owner) ? context.Key : null;
        }

        public override IApplicationLicenseKey GetLicense(ILicenseProfile profile, ref bool additionHandled, Window owner)
        {
            var context = new DialogContext(DialogType.UserNamePassword, new MineCraftLicenseKey(), profile);
            return context.Display(owner) ? context.Key : null;
        }

        public override void Install(IApplicationLicense license, IExecutionContext context, ref bool forceCreation)
        {
            if (context.HasCompleted | context.AutoLaunch)
            {
                var key = license.KeyAs<MineCraftLicenseKey>();

                #region VALIDATION

                if (key == null)
                    throw new ArgumentNullException("Key");

                if (String.IsNullOrWhiteSpace(key.Username))
                    throw new ArgumentNullException("Username");

                if (String.IsNullOrWhiteSpace(key.Password))
                    throw new ArgumentNullException("Password");

                #endregion

                #region AUTH
                var auth = new AuthenticateRequest();
                auth.ClientToken = Guid.NewGuid().ToString();
                auth.Username = key.Username;
                auth.Password = key.Password;

                WebRequest request = WebRequest.Create(api_path);
                request.ContentType = "application/json";
                request.Method = "POST";

                try
                {
                    using (var requestStream = request.GetRequestStream())
                    {
                        string input = JsonConvert.SerializeObject(auth);
                        byte[] requestPayload = Encoding.Default.GetBytes(input);
                        requestStream.Write(requestPayload, 0, requestPayload.Length);
                    };
                }
                catch (WebException)
                {
                    throw;
                }
                #endregion

                #region RESPONSE
                AuthenticateRespnse authResponse;

                try
                {
                    var response = request.GetResponse();
                    using (var responseStream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            authResponse = JsonConvert.DeserializeObject<AuthenticateRespnse>(reader.ReadToEnd());
                        }
                    }
                }
                catch (WebException)
                {
                    throw;
                }
                #endregion

                #region PROFILE
                var selectedProfile = authResponse.SelectedProfile;

                if (selectedProfile == null)
                {
                    selectedProfile = new MinecraftProfile();
                    selectedProfile.Id = Guid.NewGuid().ToString();
                    selectedProfile.Name = "Demo";
                }

                //create profile
                var launcherProfile = new LauncherProfile();
                launcherProfile.ClientToken = authResponse.ClientToken;
                launcherProfile.SelectedProfile = selectedProfile.Name;

                //create profile entry
                launcherProfile.Profiles.Add(selectedProfile.Name, new LauncherUserProfile() { Name = selectedProfile.Name, PlayerUUID = selectedProfile.Id });

                //create auth database entry
                launcherProfile.AuthenticationDatabase.Add(selectedProfile.Id, new Authentication()
                {
                    Username = auth.Username,
                    AccessToken = authResponse.AccessToken,
                    UUID = Guid.Parse(selectedProfile.Id).ToString(),
                    DisplayName = selectedProfile.Name
                });

                //create destination directory if required
                if (!Directory.Exists(target_directory_path))
                    Directory.CreateDirectory(target_directory_path);

                var output = JsonConvert.SerializeObject(launcherProfile, Formatting.Indented);

                //write to configuration file
                using (var launcher_profile_stream = new FileStream(target_file_path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    //clear all file contents
                    launcher_profile_stream.SetLength(0);
                    //write data
                    using (StreamWriter writer = new StreamWriter(launcher_profile_stream))
                    {
                        writer.Write(output);
                    }
                }
                #endregion
            }
        }

        public override void Uninstall(IApplicationLicense license)
        {
            if (File.Exists(target_file_path))
            {
                try
                {
                    //delete configuration file
                    File.Delete(target_file_path);
                }
                catch
                { }
            }
        } 

        #endregion
    }

    [Serializable()]
    public class MineCraftLicenseKey : UserNamePasswordLicenseKeyBase
    { }

    #region LauncherProfile
    [Serializable()]
    [DataContract()]
    public class LauncherProfile
    {
        public LauncherProfile()
        {
            this.Profiles = new Dictionary<string, LauncherUserProfile>();
            this.AuthenticationDatabase = new Dictionary<string, Authentication>();
        }

        [DataMember(Name = "profiles", Order = 0)]
        public Dictionary<string, LauncherUserProfile> Profiles
        {
            get;
            set;
        }

        [DataMember(Name = "selectedProfile", Order = 1)]
        public string SelectedProfile
        {
            get;
            set;
        }

        [DataMember(Name = "clientToken", Order = 2)]
        public string ClientToken
        {
            get;
            set;
        }

        [DataMember(Name = "authenticationDatabase", Order = 3)]
        public Dictionary<string, Authentication> AuthenticationDatabase
        {
            get;
            set;
        }
    } 
    #endregion

    #region Authentication
    [Serializable()]
    [DataContract()]
    public class Authentication
    {
        [DataMember(Name = "username")]
        public string Username
        {
            get;
            set;
        }

        [DataMember(Name = "accessToken")]
        public string AccessToken
        {
            get;
            set;
        }

        [DataMember(Name = "uuid")]
        public string UUID
        {
            get;
            set;
        }

        [DataMember(Name = "displayName")]
        public string DisplayName
        {
            get;
            set;
        }
    } 
    #endregion

    #region LauncherUserProfile
    [Serializable()]
    [DataContract()]
    public class LauncherUserProfile
    {
        [DataMember(Name = "name")]
        public string Name
        {
            get;
            set;
        }

        [DataMember(Name = "playerUUID")]
        public string PlayerUUID
        {
            get;
            set;
        }
    }
    #endregion

    #region AuthenticateRequest
    [DataContract()]
    [Serializable()]
    public class AuthenticateRequest
    {
        public AuthenticateRequest()
        {
            this.Agent = new Agent();
            this.Agent.Version = 1;
            this.Agent.Name = "Minecraft";
        }

        [DataMember(Name = "agent")]
        public Agent Agent
        {
            get;
            set;
        }

        [DataMember(Name = "username")]
        public string Username
        {
            get;
            set;
        }

        [DataMember(Name = "password")]
        public string Password
        {
            get;
            set;
        }

        [DataMember(Name = "clientToken")]
        public string ClientToken
        {
            get;
            set;
        }
    }
    #endregion

    #region AuthenticateRespnse
    [Serializable()]
    [DataContract()]
    public class AuthenticateRespnse
    {
        public AuthenticateRespnse()
        {
            this.AvailableProfiles = new List<MinecraftProfile>();
        }

        [DataMember(Name = "availableProfiles")]
        public List<MinecraftProfile> AvailableProfiles
        {
            get;
            set;
        }

        [DataMember(Name = "selectedProfile")]
        public MinecraftProfile SelectedProfile
        {
            get;
            set;
        }

        [DataMember(Name = "accessToken")]
        public string AccessToken
        {
            get;
            set;
        }

        [DataMember(Name = "clientToken")]
        public string ClientToken
        {
            get;
            set;
        }
    }
    #endregion

    #region Agent
    [DataContract()]
    [Serializable()]
    public class Agent
    {
        [DataMember(Name = "name")]
        public string Name
        {
            get;
            set;
        }

        [DataMember(Name = "version")]
        public int Version
        {
            get;
            set;
        }
    }
    #endregion

    #region MinecraftProfile
    [Serializable()]
    [DataContract()]
    public class MinecraftProfile
    {
        [DataMember(Name = "id")]
        public string Id
        {
            get;
            set;
        }

        [DataMember(Name = "name")]
        public string Name
        {
            get;
            set;
        }
    }
    #endregion
}
