using System;
using SharedLib;
using System.Windows.Controls;
using System.Windows;
using IntegrationLib;
using System.Threading;
using SkinInterfaces;

namespace BaseLmPlugin
{
    #region Enumerations
    public enum DialogType
    {
        Instance,
        Process,
        Executable,
        Steam,
        Registry,
        UserNamePassword,
        RegistryImport,
    }
    #endregion

    #region Dialog Context
    public class DialogContext : PropertyChangedNotificator
    {
        #region FIELDS
        private UserControl content;
        private IApplicationLicenseKey key;
        private ILicenseProfile profile;
        private SimpleCommand<object, object> acceptCommand, cancelCommand;
        private Window dialog;
        #endregion

        #region PROPERTIES

        #region COMMANDS

        public SimpleCommand<object, object> AcceptCommand
        {
            get
            {
                if (this.acceptCommand == null)
                    this.acceptCommand = new SimpleCommand<object, object>(OnCanAcceptCommand, OnAcceptCommand);               
                return this.acceptCommand;
            }
        }

        public SimpleCommand<object, object> CancelCommand
        {
            get
            {
                if (this.cancelCommand == null)
                    this.cancelCommand = new SimpleCommand<object, object>(OnCanCancelCommand, OnCancelCommand);              
                return this.cancelCommand;
            }
        } 

        #endregion

        public UserControl Content
        {
            get { return this.content; }
            protected set { this.content = value; }
        }

        public IApplicationLicenseKey Key
        {
            get { return this.key; }
            protected set { this.key = value; }
        }

        public ILicenseProfile Profile
        {
            get { return this.profile; }
            protected set { this.profile = value; }
        }

        private Window Dialog
        {
            get { return this.dialog; }
            set
            {
                this.dialog = value;
                this.RaisePropertyChanged("Dialog");
            }
        }

        #endregion

        #region CONSTRUCTOR
        public DialogContext(DialogType type, IApplicationLicenseKey key, ILicenseProfile profile)
        {
            this.Key = key;
            this.Profile = profile;
            switch (type)
            {
                case DialogType.Steam:
                      this.Content = new AddSteamKey();
                    break;
                case DialogType.UserNamePassword:
                    this.Content = new AddSteamKey(false);
                    break;
                case DialogType.Executable:
                case DialogType.Process:
                    this.Content = new AddProcessKey();
                    break;
                case DialogType.Registry:
                    this.Content = new AddRegistryKey();
                    break;
                case DialogType.RegistryImport:
                    this.Content = new RegistryImportKey();
                    break;
                default: break;
            }
        }
        #endregion

        #region FUNCTIONS

        public bool Display(Window owner)
        {
            this.Dialog = new KeyDialog();
            this.Dialog.DataContext = this;
            this.Dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Dialog.Owner = owner;          
            return (bool)this.Dialog.ShowDialog();
        }

        #endregion

        #region COMMAND IMPLEMENTATION

        private bool OnCanAcceptCommand(object param)
        {
            return this.Dialog != null;
        }

        private bool OnCanCancelCommand(object param)
        {
            return this.Dialog != null;
        }

        private void OnCancelCommand(object param)
        {
            this.Dialog.DialogResult = false;
            this.Dialog.Close();
        }

        private void OnAcceptCommand(object param)
        {
            this.Dialog.DialogResult = true;
            this.Dialog.Close();
        }

        #endregion
    }
    #endregion

    #region UserNamePasswordLicenseKeyBase
    [Serializable()]
    public class UserNamePasswordLicenseKeyBase : ApplicationLicenseKeyBase
    {
        #region Fields
        private string
            username,
            password;
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets licenses username.
        /// </summary>
        public string Username
        {
            get { return this.username; }
            set
            {
                this.username = value;
                this.RaisePropertyChanged("Username");
            }
        }

        /// <summary>
        /// Gets or sets license password.
        /// </summary>
        public string Password
        {
            get { return this.password; }
            set
            {
                this.password = value;
                this.RaisePropertyChanged("Password");
            }
        }

        /// <summary>
        /// Gets if license is valid.
        /// </summary>
        public override bool IsValid
        {
            get
            {
                return !((String.IsNullOrWhiteSpace(this.Username) & (String.IsNullOrWhiteSpace(this.Password))));
            }
        }

        /// <summary>
        /// Gets license literal string representation.
        /// </summary>
        public override string KeyString
        {
            get
            {
                return String.Format("Username:{0} | Password:{1}",
                    !String.IsNullOrWhiteSpace(this.Username) ? this.Username : "Invalid Username",
                    !String.IsNullOrWhiteSpace(this.Password) ? this.Password : "Invalid Password");
            }
        }

        #endregion

        #region Ovveride
        public override string ToString()
        {
            return this.KeyString;
        }
        #endregion
    }
    #endregion    

    #region Classes
    public class TerminateWaitHandle : ManualResetEventSlim
    {
        #region Constructor
        public TerminateWaitHandle(bool initialState)
            : base(initialState)
        { } 
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets if handle is waiting for termination.
        /// </summary>
        public bool WaitingTermination
        {
            get;
            internal set;
        }
        /// <summary>
        /// Gets process name.
        /// </summary>
        public string TerminatingProcesName
        {
            get;
            internal set;
        } 
        #endregion
    }
    #endregion
}
