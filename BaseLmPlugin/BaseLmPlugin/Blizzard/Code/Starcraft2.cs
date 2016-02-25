using System;
using IntegrationLib;
using System.ComponentModel.Composition;
using System.Drawing;
using GizmoShell;
using Win32API.Modules;

namespace BaseLmPlugin
{
    #region Starcraft2LicenseManagerPlugin
    [PartNotDiscoverable()]
    [Export(typeof(ILicenseManagerPlugin))]
    [PluginMetadata("Starcraft II [BETA]",
        "1.0.0.0",
        "Manages license keys by sending credentials input to application window.",
        "BaseLmPlugin;BaseLmPlugin.Resources.Icons.starcraft2.png")]
    public class Starcraft2LicenseManagerPlugin : Diablo3LicenseManagerPlugin
    {
        #region Constructor
        public Starcraft2LicenseManagerPlugin()
        {
            #region Configuration
            this.TargetProcessName = "SC2";
            this.TargetWindowName = "StarCraft II";
            this.SendWaitTime = 3000;
            #endregion

            #region PIXEL-MAP
            this.Pixels.Clear();
            this.Pixels.Add(ColorTranslator.FromHtml("#030E23"));
            this.Pixels.Add(ColorTranslator.FromHtml("#030F24"));
            this.Pixels.Add(ColorTranslator.FromHtml("#021126"));
            this.Pixels.Add(ColorTranslator.FromHtml("#000F22"));
            this.Pixels.Add(ColorTranslator.FromHtml("#011025"));
            this.Pixels.Add(ColorTranslator.FromHtml("#021227"));
            this.Pixels.Add(ColorTranslator.FromHtml("#031026"));
            this.Pixels.Add(ColorTranslator.FromHtml("#010F23"));
            this.Pixels.Add(ColorTranslator.FromHtml("#010E22"));
            this.Pixels.Add(ColorTranslator.FromHtml("#030F23"));

            this.Pixels.Add(ColorTranslator.FromHtml("#171518"));
            this.Pixels.Add(ColorTranslator.FromHtml("#151316"));
            this.Pixels.Add(ColorTranslator.FromHtml("#151317"));
            this.Pixels.Add(ColorTranslator.FromHtml("#17171E"));
            this.Pixels.Add(ColorTranslator.FromHtml("#0E0D10"));
            this.Pixels.Add(ColorTranslator.FromHtml("#0F0C10"));
            this.Pixels.Add(ColorTranslator.FromHtml("#151318"));
            this.Pixels.Add(ColorTranslator.FromHtml("#141014"));
            this.Pixels.Add(ColorTranslator.FromHtml("#110C10"));
            this.Pixels.Add(ColorTranslator.FromHtml("#251C24"));

            this.Pixels.Add(ColorTranslator.FromHtml("#63C8EF"));
            this.Pixels.Add(ColorTranslator.FromHtml("#71D2F5"));
            this.Pixels.Add(ColorTranslator.FromHtml("#75D4F7"));
     
            #endregion
        }
        #endregion

        #region OVERRIDES

        protected override UserNamePasswordLicenseKeyBase GetKeyInstance()
        {
            return new Starcraft2Key();
        }

        protected override void OnSendInput(IntPtr hwnd, string username, string password)
        {
            #region Validation
            if (hwnd == IntPtr.Zero)
            {
                throw new ArgumentException("Hwnd", "Invalid window handle specified.");
            }
            if (String.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentNullException("Username", "Invalid username specified.");
            }
            if (String.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentNullException("Password", "Invalid password specified.");
            }
            #endregion

            //get window info
            WindowInfo window = new WindowInfo(hwnd);

            //check if window is valid
            if (window.IsValidWindow)
            {

                try
                {
                    //block user input
                    User32.BlockInput(true);

                    //restore window (case it minimized)
                    window.Restore(false);

                    //bring window to front
                    window.BringToFront();

                    //activate window
                    window.Activate();

                    //create input simulator
                    WindowsInput.KeyboardSimulator sim = new WindowsInput.KeyboardSimulator();

                    //send tab
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.TAB);

                    //clear username filed
                    sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                    //send back to clear any possible typed value
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                    //send username
                    sim.TextEntry(username);

                    //initiate auth
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.RETURN);

                    //wait for login
                    if (this.SendWaitTime > 0)
                    {
                        System.Threading.Thread.Sleep(this.SendWaitTime);
                    }

                    //clear password filed
                    sim.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LCONTROL, WindowsInput.Native.VirtualKeyCode.VK_A);

                    //send back to clear any possible typed value
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.BACK);

                    //send bassword
                    sim.TextEntry(password);

                    //initiate login
                    sim.KeyDown(WindowsInput.Native.VirtualKeyCode.RETURN);

                    if (this.SendWaitTime > 0)
                    {
                        //sleep few seconds to allow login
                        System.Threading.Thread.Sleep(this.SendWaitTime);
                    }
                }
                catch
                {
                    //ignore
                }
                finally
                {

                    //unblock input
                    User32.BlockInput(false);
                }
            }
        }

        #endregion
    } 
    #endregion

    #region Starcraft2Key
    [Serializable()]
    public class Starcraft2Key : UserNamePasswordLicenseKeyBase
    {
    }
    #endregion
}
