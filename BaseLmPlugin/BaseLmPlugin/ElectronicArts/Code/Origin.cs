using System;
using System.Collections.Generic;
using System.Linq;
using IntegrationLib;
using System.ComponentModel.Composition;
using SharedLib;
using System.IO;
using Client;
using System.Windows;
using System.Diagnostics;
using CoreLib.Imaging;
using System.Drawing;
using Microsoft.Win32;
using CoreLib.Diagnostics;
using GizmoShell;
using System.Xml;
using Win32API.Modules;

namespace BaseLmPlugin
{
    #region OriginLicenseManager
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("EA Origin","1.0.0.0","Manages license keys by sending credentials input to application window.","BaseLmPlugin;BaseLmPlugin.Resources.Icons.origin.png")]
    public class OriginLicenseManager : SteamLicenseManager
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
            var context = new DialogContext(DialogType.UserNamePassword, new OriginLicenseKey(), profile);
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
                string originPath = this.GetOriginPath();

                if (String.IsNullOrWhiteSpace(originPath))
                {
                    context.Client.Log.AddError("Could not obtain Origin client executable path.", null, LogCategories.Configuration);
                    return;
                }

                if(!File.Exists(originPath))
                {
                    context.Client.Log.AddError(String.Format("Origin client executable not found at {0}.", originPath), null, LogCategories.Configuration);
                    return;
                }

                #endregion

                #region Clear XML Configuration

                //get folder name
                string folderName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Origin");

                //get file name
                string fileName = Path.Combine(folderName, "local.xml");

                if (!Directory.Exists(folderName))
                    Directory.CreateDirectory(folderName);

                //create new stream and write or update its configuration data
                using (var xmlStream = new FileStream(fileName, FileMode.OpenOrCreate))
                {
                    XmlDocument document = new XmlDocument();
                    //preserve white space
                    document.PreserveWhitespace = true;

                    try
                    {
                        document.Load(xmlStream);
                    }
                    catch (XmlException)
                    {
                        //could not load
                    }

                    XmlNode settingsNode = null;

                    #region Find Settings Node
                    if (document.HasChildNodes)
                    {
                        foreach (XmlNode node in document.ChildNodes)
                        {
                            if (node.Name == "Settings")
                            {
                                settingsNode = node;
                                break;
                            }
                        }
                    }
                    #endregion

                    #region Create Settings Node
                    if (settingsNode == null)
                    {
                        settingsNode = document.CreateElement("Settings");
                        document.AppendChild(settingsNode);
                    }
                    #endregion

                    bool isSet = false;

                    #region Update Existing Node
                    if (settingsNode.HasChildNodes)
                    {
                        foreach (XmlLinkedNode el in settingsNode.ChildNodes)
                        {
                            if (el is XmlElement)
                            {
                                var element = (XmlElement)el;
                                if (element.HasAttribute("key") && element.Attributes["key"].Value == "AcceptedEULAVersion")
                                {
                                    if (element.Attributes["value"].Value == "0")
                                        element.Attributes["value"].Value = (2).ToString();

                                    isSet = true;
                                    break;
                                }
                            }
                        }
                    }
                    #endregion

                    #region Clear Additional Settings
                    if (settingsNode.HasChildNodes)
                    {
                        List<XmlElement> removedElements = new List<XmlElement>();
                        foreach (XmlLinkedNode el in settingsNode.ChildNodes)
                        {
                            if (el is XmlElement)
                            {
                                var element = (XmlElement)el;
                                if (element.HasAttribute("key") &&
                                    element.Attributes["key"].Value == "AutoLogin" ||
                                    element.Attributes["key"].Value == "LoginAsInvisible" ||
                                    element.Attributes["key"].Value == "LoginEmail" ||
                                    element.Attributes["key"].Value == "LoginToken" ||
                                    element.Attributes["key"].Value == "RememberMeEmail")
                                {
                                    removedElements.Add(element);
                                }
                            }
                        }

                        foreach (var element in removedElements)
                        {
                            settingsNode.RemoveChild(element);
                        }
                    }
                    #endregion

                    #region Create new node
                    if (!isSet)
                    {
                        var setting = document.CreateElement("Setting");
                        settingsNode.AppendChild(setting);
                        setting.SetAttribute("key", "AcceptedEULAVersion");
                        setting.SetAttribute("type", "2");
                        setting.SetAttribute("value", "2");
                    }
                    #endregion

                    //reset stream
                    xmlStream.SetLength(0);

                    //save document
                    document.Save(xmlStream);
                }
                #endregion

                string processName = Path.GetFileNameWithoutExtension(originPath);          

                #region Initialize Process

                //get existing origin process
                var originProcess = Process.GetProcessesByName(processName).Where(x => String.Compare(x.MainModule.FileName, originPath, true) == 0).FirstOrDefault();

                bool processExisted = originProcess != null;

                if(!processExisted)
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = originPath;
                    startInfo.Arguments = "/NoEULA";
                    startInfo.WorkingDirectory = Path.GetDirectoryName(originPath);
                    startInfo.ErrorDialog = false;
                    startInfo.UseShellExecute = false;

                    //create origin process
                    originProcess = new Process() { StartInfo = startInfo };
                }
     
                originProcess.EnableRaisingEvents = true;
                originProcess.Exited += new EventHandler(OnInternalProcessExited);

                #endregion

                #region Start Origin
                if (processExisted || originProcess.Start())
                {
                    //mark process created
                    forceCreation = true;

                    //atach handlers
                    context.ExecutionStateChaged += OnExecutionStateChaged;

                    //add process to context process list
                    context.AddProcess(originProcess, true);

                    if (CoreProcess.WaitForWindowCreated(originProcess, 120000, true))
                    {
                        try
                        {
                            IntPtr mainWindow = originProcess.MainWindowHandle;

                            //create input simulator
                            WindowsInput.KeyboardSimulator sim = new WindowsInput.KeyboardSimulator();

                            if (!this.FocusField(originProcess, OriginInputFileds.Username, 50))
                                return;

                            //clear username filed
                            sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                            //send back to clear any possible typed value
                            sim.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);

                            //set username
                            sim.TextEntry(license.KeyAs<UserNamePasswordLicenseKeyBase>().Username);

                            if (!this.FocusField(originProcess, OriginInputFileds.Password, 50))
                                return;

                            //clear password filed
                            sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                            //send back to clear any possible typed value
                            sim.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);

                            //set password
                            sim.TextEntry(license.KeyAs<UserNamePasswordLicenseKeyBase>().Password);

                            //proceed with login
                            sim.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);

                            //set environment variable
                            Environment.SetEnvironmentVariable("LICENSEKEYUSER", license.KeyAs<OriginLicenseKey>().Username);

                            //wait for window to be destroyed
                            if (CoreProcess.WaitForWindowDestroyed(mainWindow, 120000))
                                //delay installation process
                                System.Threading.Thread.Sleep(3000);
                        }
                        catch
                        {
                            throw;
                        }
                    }
                    else
                    {
                        context.Client.Log.AddError("Origin client window was not created after specified period of time.", null, LogCategories.Configuration);
                    }
                }
                else
                {
                    context.Client.Log.AddError(String.Format("Origin client executable {0} could not be started.", originPath), null, LogCategories.Configuration);
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
            return new OriginLicenseManagerSettings();
        }

        public override bool DivertExecution(IExecutionContext context)
        {
            return false;
        }

        #endregion

        #region Private

        private string GetOriginPath()
        {
            string originPath = string.Empty;
            using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(@"SOFTWARE\Origin\", false))
            {
                if (key != null)
                    originPath = key.GetValue("ClientPath").ToString();                
            }
            return originPath;
        }

        private int GetInitialSteps(OriginInputFileds field)
        {
            switch (field)
            {
                case OriginInputFileds.Password:
                    return 5;
                case OriginInputFileds.Connect:
                case OriginInputFileds.ForgotPassword:
                    return 4;
                case OriginInputFileds.StayConnected:
                    return 3;
                case OriginInputFileds.Invisible:
                    return 2;
                case OriginInputFileds.CreateAccount:
                    return 1;
                case OriginInputFileds.None:
                    return 2;
                default: return 0;
            }
        }

        private bool GetFields(Image image, out OriginInputFileds focusedField, out OriginInputFileds checkedField)
        {
            focusedField = OriginInputFileds.None;
            checkedField = OriginInputFileds.None;

            try
            {
                #region Get UI Fields State
                using (var tr = new ImageTraverser(image))
                {
                    bool usernameFocused = tr[45, 187] == ColorTranslator.FromHtml("#EDBD69").ToArgb();
                    bool passwordFocused = tr[45, 252] == ColorTranslator.FromHtml("#EDBD69").ToArgb();
                    bool keepPasswordFocused = tr[45, 370] == ColorTranslator.FromHtml("#EEBD67").ToArgb();
                    bool keepPasswordChecked = tr[54, 366] == ColorTranslator.FromHtml("#FF9900").ToArgb();
                    bool invisibleFocused = tr[45, 367] == ColorTranslator.FromHtml("#EEBD67").ToArgb();
                    bool invisibleChecked = tr[54, 394] == ColorTranslator.FromHtml("#FF9900").ToArgb();
                    bool forgotPasswordFocused = tr[200, 271] == ColorTranslator.FromHtml("#000000").ToArgb();
                    bool createAccountFocused = tr[142, 447] == ColorTranslator.FromHtml("#000000").ToArgb();

                    if (passwordFocused) { focusedField = OriginInputFileds.Password; }
                    if (usernameFocused) { focusedField = OriginInputFileds.Username; }
                    if (forgotPasswordFocused) { focusedField = OriginInputFileds.ForgotPassword; }
                    if (keepPasswordFocused) { focusedField = OriginInputFileds.StayConnected; }
                    if (invisibleFocused) { focusedField = OriginInputFileds.Invisible; }
                    if (createAccountFocused) { focusedField = OriginInputFileds.CreateAccount; }
                    if (keepPasswordChecked) { checkedField = checkedField ^ OriginInputFileds.StayConnected; }
                    if (invisibleChecked) { checkedField = checkedField ^ OriginInputFileds.Invisible; }

                    tr.Dispose();
                    return true;
                }
                #endregion
            }
            catch
            {
                return false;
            }
        }

        private bool FocusField(Process originProcess, OriginInputFileds field ,int tries)
        {
            WindowInfo window = new WindowInfo(originProcess.MainWindowHandle);
            WindowsInput.KeyboardSimulator sim = new WindowsInput.KeyboardSimulator();
         
            for (int i = 0; i < tries; i++)
            {
                try
                {
                    User32.BlockInput(true);                    

                    //reactivate window
                    window.BringToFront();
                    window.Activate();

                    //create input variables
                    OriginInputFileds focusedField = OriginInputFileds.None;
                    OriginInputFileds checkedField = OriginInputFileds.None;

                    bool aero_on =false ;

                    //check if aero is on
                    if (Environment.OSVersion.Version.Major > 5)
                        Win32API.Modules.DwmApi.DwmIsCompositionEnabled(out aero_on);
                 
                    //grab snapshot
                    var image =aero_on? Imaging.CaptureScreenRegion(originProcess.MainWindowHandle) : Imaging.CaptureWindowImage(originProcess.MainWindowHandle);

                    if (this.GetFields(image, out focusedField, out checkedField))
                    {
                        if (focusedField == field)
                            return true;
                    }

                    //send tab
                    sim.KeyPress(WindowsInput.Native.VirtualKeyCode.TAB);

                    //pause
                    System.Threading.Thread.Sleep(250);
                }
                catch
                {
                    throw;
                }
                finally
                {
                    User32.BlockInput(false);
                }
            }
            return false;
        }

        #endregion
    }
    #endregion

    #region OriginLicenseManagerSettings
    [Serializable]
    public class OriginLicenseManagerSettings : SteamLicenseManagerSettings, IPluginSettings
    {
    }
    #endregion

    #region OriginLicenseKey
    [Serializable()]
    public class OriginLicenseKey : UserNamePasswordLicenseKeyBase
    {

    }
    #endregion

    #region OriginInputFileds
    [Flags()]
    public enum OriginInputFileds : int
    {
        None = 0,
        Username = 1,
        Password = 2,
        ForgotPassword = 4,
        Connect = 8,
        StayConnected = 16,
        Invisible = 32,
        CreateAccount = 64,
    }
    #endregion
}
