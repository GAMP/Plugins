using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IntegrationLib;
using System.ComponentModel.Composition;
using SharedLib;
using System.IO;
using Client;
using System.Windows;
using System.Diagnostics;
using System.Threading;
using CoreLib.Imaging;
using System.Drawing;
using Microsoft.Win32;
using CoreLib.Diagnostics;
using GizmoShell;
using System.Xml;

namespace BaseLmPlugin
{
    #region OriginLicenseManager
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("EA Origin",
        "1.0.0.0",
        "Manages license keys by sending credentials input to application window.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.origin.png")]
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

                if (String.IsNullOrWhiteSpace(originPath) || !File.Exists(originPath))
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
                    catch (XmlException ex)
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

                #region Initialize Process
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = originPath;
                startInfo.WorkingDirectory = Path.GetDirectoryName(originPath);
                startInfo.ErrorDialog = false;
                startInfo.UseShellExecute = false;

                //start origin process
                var originProcess = new Process() { StartInfo = startInfo };
                originProcess.EnableRaisingEvents = true;
                originProcess.Exited += new EventHandler(OnInternalProcessExited);
                #endregion

                #region Start Origin
                if (originProcess.Start())
                {
                    //mark process created
                    forceCreation = true;

                    //atach handlers
                    context.ExecutionStateChaged += new ExecutionContextStateChangedDelegate(OnExecutionStateChaged);

                    //add process to context process list
                    context.AddProcess(originProcess, true);

                    if (CoreProcess.WaitForWindowCreated(originProcess, 10000, true))
                    {
                        try
                        {
                            //disable input
                            Win32API.Modules.CS.User32.BlockInput(true);

                            //get window
                            WindowInfo originWindow = new WindowInfo(originProcess.MainWindowHandle);

                            OriginInputFileds focusedField = OriginInputFileds.None;
                            OriginInputFileds checkedField = OriginInputFileds.None;

                            //activate origin window
                            originWindow.BringToFront();
                            originWindow.Activate();

                            //give some time to activate fields
                            System.Threading.Thread.Sleep(1000);

                            for (int i = 0; i <= 5; i++)
                            {
                                //grab snapshot
                                var image = CoreLib.Imaging.Imaging.CaptureWindowImage(originProcess.MainWindowHandle);
                                if (this.GetFields(image, ref focusedField, ref checkedField))
                                {
                                    if (focusedField != OriginInputFileds.None)
                                        break;
                                }
                            }

                            //create input simulator
                            WindowsInput.KeyboardSimulator sim = new WindowsInput.KeyboardSimulator();

                            for (int steps = this.GetInitialSteps(focusedField); steps > 0; steps--)
                            {
                                //tab to first field
                                sim.KeyDown(WindowsInput.Native.VirtualKeyCode.TAB);
                                //wait between tabs
                                System.Threading.Thread.Sleep(100);
                            }

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
                            Environment.SetEnvironmentVariable("LICENSEKEYUSER", license.KeyAs<OriginLicenseKey>().Username);

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

        protected override void OnReportTerminate(IExecutionContext context)
        {
            context.WriteMessage(String.Format("Process {0} caused origin to exit.", this.TerminateHandle.TerminatingProcesName));
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
                default: return 0;
            }
        }

        private bool GetFields(Image image, ref OriginInputFileds focusedField, ref OriginInputFileds checkedField)
        {
            try
            {
                #region Get UI Fields State
                using (var tr = new ImageTraverser(image))
                {
                    bool usernameFocused = tr[52, 145] == ColorTranslator.FromHtml("#EDBD69").ToArgb();
                    bool passwordFocused = tr[52, 203] == ColorTranslator.FromHtml("#EDBD69").ToArgb();
                    bool keepPasswordFocused = tr[52, 310] == ColorTranslator.FromHtml("#EEBD67").ToArgb();
                    bool keepPasswordChecked = tr[55, 319] == ColorTranslator.FromHtml("#FFB950").ToArgb();
                    bool invisibleFocused = tr[52, 336] == ColorTranslator.FromHtml("#EEBD67").ToArgb();
                    bool invisibleChecked = tr[55, 346] == ColorTranslator.FromHtml("#FFB950").ToArgb();
                    bool forgotPasswordFocused = tr[122, 235] == ColorTranslator.FromHtml("#106487").ToArgb();
                    bool createAccountFocused = tr[107, 400] == ColorTranslator.FromHtml("#FFB071").ToArgb();

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
