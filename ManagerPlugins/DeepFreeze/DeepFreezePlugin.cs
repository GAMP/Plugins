using CoreLib;
using CoreLib.Diagnostics;
using IntegrationLib;
using Manager.Modules;
using Manager.Services;
using Manager.ViewModels;
using Manager.Views;
using SharedLib.Dispatcher.Exceptions;
using SharedLib.Services;
using SharedLib.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ManagerPlugins
{
    /// <summary>
    /// Deep Freeze manager module.
    /// </summary>
    [PluginMetadata("Deep freeze", "1.0.0.0")]
    [Export(typeof(IManagerModule))]
    public class DeepFreezePlugin : ManagerModulePluginBase
    {
        #region IMPORTS

        [Import()]
        private IViewModelLocator<IHostComputerManagementViewModel> HostLocator
        {
            get; set;
        }

        [Import()]
        private IMultiSelectSelectionProvider<IHostComputerManagementViewModel> Selector
        {
            get; set;
        }

        [Import()]
        private IHostManagementModuleMainView HostView
        {
            get; set;
        }

        [Import()]
        private IServicesManagerService ServiceManager
        {
            get; set;
        }

        [Import()]
        private IManagerMenuService MenuService
        {
            get; set;
        }

        [Import()]
        private ILocalizationService LocalizationService
        {
            get; set;
        }

        [Import()]
        private ICoreProcessFactory ProcessFactory
        {
            get; set;
        }

        [Import()]
        private ICyFileSystemFactory FileFactory
        {
            get; set;
        }

        [Import()]
        private IManagerLogService LogService
        {
            get; set;
        }

        [Import()]
        private IManagerMessageService MessageService
        {
            get; set;
        }

        #endregion

        #region FIELDS

        private string DFC_ARGUMENTS_BASE = "{0} /{1}";
        private string DEEP_FREEZE_PASWORD;
        private string DFC_x64_PATH = @"%windir%\SysWOW64\DFC.exe";
        private string DFC_x32_PATH = @"%windir%\System32\DFC.exe";

        private readonly string PROPERTY_DEEP_FREEZE_ENABLED = "DeepFreezeEnabled";
        private readonly string PROPERTY_DEEP_FREEZE_INSTALLED = "DeepFreezeInstalled";
        private readonly string PROPERTY_IS_WORKING = "IsWorking";

        private SemaphoreSlim OP_LOCK = new SemaphoreSlim(1, 1);

        #endregion

        #region FIELDS

        //CREATE UNIQUE STRING KEYS FOR YOUR MENU ITEMS, GUIDS CAN SERVER A PERFECT ROLE FOR THAT

        private readonly string DF_ROOT_MENU_KEY = "a481bf50-4b91-49db-8181-bddbe17d6ca3";
        private readonly string DF_MENU_FREEZE = "aee3d0a8-52f5-4f77-9754-36b8461f06a9";
        private readonly string DF_MENU_THAW = "615d1a91-98ce-46f3-88d3-b30522a2459b";
        private readonly string DF_MENU_THAW_LOCKED_NEXT_BOOT = "d74cd683-70b2-478e-a77b-2302c28ca098";

        #endregion

        #region PROPERTIES

        private bool IsPasswordSet
        {
            get; set;
        }

        #endregion

        #region FUNCTIONS

        private string GetArguments(DFCCommand command)
        {
            return this.GetArguments(DEEP_FREEZE_PASWORD, command.ToString());
        }

        private string GetArguments(string password, string command)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException(nameof(password));

            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentNullException(nameof(command));

            return string.Format(DFC_ARGUMENTS_BASE, password, command);
        }

        private ICoreProcessStartInfo CreateStartInfo(DFCCommand command,bool? isX64os)
        {
            var startInfo = this.ProcessFactory.CreateStartInfo();            
            startInfo.FileName = isX64os == true ? DFC_x64_PATH : DFC_x32_PATH;
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(DFC_x64_PATH);
            startInfo.WaitForTermination = true;
            startInfo.Arguments = GetArguments(command);
            return startInfo;
        }

        private async Task UpdateHostState()
        {
            try
            {
                if (await OP_LOCK.WaitAsync(TimeSpan.Zero))
                {
                    try
                    {
                        var computers = this.HostLocator.EnumerableSource
                            .Where(host => host.DispatcherId != null)
                            .Select(host => host.Id)
                            .ToList();

                        var TASKS = computers.Select(hostId => this.UpdateHostState(hostId));

                        await Task.WhenAll(TASKS);
                    }
                    catch
                    {
                        throw;
                    }
                    finally
                    {
                        OP_LOCK.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                await this.MessageService.ShowExceptionMessageAsync(ex);
            }
        }

        private async Task UpdateHostState(int hostId)
        {
            try
            {
                //password was not set by plugin
                if (!this.IsPasswordSet)
                    return;

                //get current service instance
                var service = this.ServiceManager.Current;

                //check if we can call current service
                if (service == null || !service.IsConnected || !service.IsAuthenticated)
                    return;

                //get view model
                var hostComputer = this.HostLocator.TryGetViewModel(hostId);

                if (hostComputer is IDynamicPropertyObject dynamic)
                {
                    var dispatcherId = hostComputer.DispatcherId;

                    if (!dispatcherId.HasValue)
                        return;

                    bool isX64OS = hostComputer.Is64BitOperatingSystem == true;
                    string DFC_PATH = isX64OS ? DFC_x64_PATH : DFC_x32_PATH;

                    var dispatcher = service.ProxyDispatcherGet(dispatcherId.Value);

                    var IS_INSTALLED = await FileFactory.FileExistsAsync(DFC_PATH, dispatcher);

                    dynamic.SetPropertyValue(PROPERTY_DEEP_FREEZE_INSTALLED, IS_INSTALLED);

                    if (IS_INSTALLED)
                    {
                        //create start info
                        var startInfo = this.CreateStartInfo(DFCCommand.ISFROZEN, isX64OS);

                        //start process
                        var result = await ProcessFactory.StartAsync(startInfo, dispatcher);

                        //exit code should be 1 if frozen
                        var IS_ENABLED = result.ExitCode == 1;

                        //set property
                        dynamic.SetPropertyValue(PROPERTY_DEEP_FREEZE_ENABLED, IS_ENABLED);
                    }
                }
            }
            catch (PoolDispatchFailedException)
            {
            }
            catch
            {
            }
            finally
            {
            }
        }
        
        private void ResetCommands()
        {
            foreach (var menu in this.MenuService.GetMenuItem(DF_ROOT_MENU_KEY).Children)
                menu?.Command?.RaiseCanExecuteChanged();
        }

        private string Loc(string key)
        {
            string fqn = this.LocalizationService.FormatFQN("ManagerPlugins", "Strings", key);
            return this.LocalizationService.LocFQN<string>(fqn);
        }

        #endregion

        #region OVERRIDES

        public override void Initialize()
        {
            base.Initialize();

            //create your menus
            var mainMenu = this.MenuService.CreateMenu(Manager.KnownMenuArea.HostManagementModule, DF_ROOT_MENU_KEY, Loc("DEEP_FREEZE"), false, "icon-deep-freeze");
            mainMenu.IsVectorIcon = false;

            this.MenuService.CreateMenu(DF_ROOT_MENU_KEY, DF_MENU_FREEZE, Loc("MENU_FREEZE"), false, null, OnCanExecuteAction, OnExecuteAction, DFAction.Freeze);
            this.MenuService.CreateMenu(DF_ROOT_MENU_KEY, DF_MENU_THAW, Loc("MENU_THAW"), false, null, OnCanExecuteAction, OnExecuteAction, DFAction.Thaw);
            this.MenuService.CreateMenu(DF_ROOT_MENU_KEY, DF_MENU_THAW_LOCKED_NEXT_BOOT, Loc("MENU_THAW_LOCKED"), false, null, OnCanExecuteAction, OnExecuteAction, DFAction.ThawLocked);

            var column = new DataGridTemplateColumn()
            {
                Header = Loc("DEEP_FREEZE")
            };

            this.HostView.Columns.Last().Width = DataGridLength.Auto;

            this.HostView.Columns.Add(column);

            var resources = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/ManagerPlugins;component/Resources/DeepFreezePluginRes.xaml", UriKind.Absolute)
            };

            column.CellTemplate = resources["_deep_freeze_display_template"] as DataTemplate;

            //add your resources to application
            //this will allow them to be resolved by other application parts
            Application.Current.Resources.MergedDictionaries.Add(resources);

            //add localization resources
            this.LocalizationService.UpdateDictionary(GetType().Assembly.FullName, "Strings");
        }

        public override void Start()
        {
            base.Start();

            foreach (var host in this.HostLocator.EnumerableSource.OfType<IDynamicPropertyObject>())
            {
                host.AddProperty<bool>(PROPERTY_DEEP_FREEZE_ENABLED);
                host.AddProperty<bool>(PROPERTY_DEEP_FREEZE_INSTALLED);
                host.AddProperty<bool>(PROPERTY_IS_WORKING);
            }

            if (Environment.GetEnvironmentVariable("DEEP_FREEZE_PASSWORD") == null)
                this.MessageService.ShowError($"{this.GetType().ToString()} {Loc("ERROR_ENVIRONMENT_VARIABLE_NOT_SET")}");
            else
                this.IsPasswordSet = true;

            DEEP_FREEZE_PASWORD = Environment.ExpandEnvironmentVariables("%DEEP_FREEZE_PASSWORD%");

            this.Selector.SelectionChanged += OnSelectionChanged;

            this.ServiceManager.Current.HostPropertiesChangeCompleted += OnHostPropertiesChange;

            this.UpdateHostState().ContinueWith((task) =>
            {
                //handle falts here
            }, continuationOptions: TaskContinuationOptions.OnlyOnFaulted);
        }
     

        public override void Stop()
        {
            base.Stop();

            this.Selector.SelectionChanged -= OnSelectionChanged;
            this.ServiceManager.Current.HostPropertiesChangeCompleted -= OnHostPropertiesChange;
        }

        #endregion

        #region EVENT HANDLERS

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ResetCommands();
        }

        private async void OnHostPropertiesChange(object sender, ServerService.HostPropertiesChangedEventArgs e)
        {
            bool shouldUpdate = e.HasProperty(SharedLib.HostPropertyType.IsConnected) && e.GetProperty<bool>(SharedLib.HostPropertyType.IsConnected)==true ||
                e.HasProperty(SharedLib.HostPropertyType.DispatcherId) && e.GetProperty<int?>(SharedLib.HostPropertyType.DispatcherId)!=null;

            if(shouldUpdate)
            {
                try
                {
                    await this.UpdateHostState(e.HostId);
                }
                catch
                {
                    //ignore
                }
            }

            if(e.HasProperty(SharedLib.HostPropertyType.IsConnected))
            {
                this.ResetCommands();
            }
        }

        #endregion

        #region COMMANDS

        private bool OnCanExecuteAction(object param)
        {
            if (!this.Selector.SelectedItems.Where(x => x.DispatcherId != null).Any())
                return false;

            return this.ServiceManager.Current != null;
        }
        private async void OnExecuteAction(object param)
        {
            if (param is DFAction action)
            {
                try
                {
                    var service = this.ServiceManager.Current;

                    if (service == null)
                        return;

                    if (!service.IsConnected)
                        return;

                    if (!service.IsAuthenticated)
                        return;

                    var LOC_ATTRIBUTE = action.GetAttributeOfType<Localization.LocalizedAttribute>();
                    var LOC_STRING = LOC_ATTRIBUTE!=null ? Loc(LOC_ATTRIBUTE.ResourceKey) : action.ToString();
                    var LOC_BASE_MESSAGE = Loc("ACTION_WARNING");
                    if (await this.MessageService.ShowWarningMessageAsync($"{LOC_BASE_MESSAGE} {LOC_STRING} ?", Manager.ManagerMessageDialogStyle.AffirmativeAndNegative) != Manager.ManagerMessageDialogResult.Affirmative)
                        return;                  

                    //get a list of selected computers
                    var selectedHosts = this.Selector.SelectedItems
                        .Where(x => x.DispatcherId != null)
                        .ToList();

                    var TASKS = selectedHosts.Select(async (host) =>
                    {
                        var dispatcherId = host.DispatcherId;
                        if (dispatcherId.HasValue)
                        {
                            if (host is IDynamicPropertyObject dynamic)
                            {
                                dynamic.SetPropertyValue(PROPERTY_IS_WORKING, true);

                                try
                                {
                                    var dispatcher = this.ServiceManager.Current.ProxyDispatcherGet(dispatcherId.Value);

                                    if (dynamic.GetPropertyValue<bool>(PROPERTY_DEEP_FREEZE_INSTALLED))
                                    {
                                        DFCCommand command = DFCCommand.BOOTFROZEN;
                                        switch (action)
                                        {
                                            case DFAction.Freeze:
                                                command = DFCCommand.BOOTFROZEN;
                                                break;
                                            case DFAction.Thaw:
                                                command = DFCCommand.BOOTTHAWED;
                                                break;
                                            case DFAction.ThawLocked:
                                                command = DFCCommand.BOOTTHAWEDNOINPUT;
                                                break;
                                            default:
                                                throw new ArgumentException();
                                        }

                                        var START_INFO = this.CreateStartInfo(command,host.Is64BitOperatingSystem ==true);
                                        var PROCESS = await ProcessFactory.StartAsync(START_INFO, dispatcher);

                                        if (PROCESS?.ExitCode > 1 & PROCESS?.ExitCode < 5)
                                            this.LogService.AddError($"Deep freeze command {command} for {host.Name} failed with error : {(DFCCommandResult)PROCESS.ExitCode}");

                                        if (PROCESS?.ExitCode >= 5)
                                            this.LogService.AddError($"Deep freeze  Internal error executing command {command} for {host.Name} exit code : {PROCESS.ExitCode}");
                                    }
                                }
                                catch (ConnectionLostException)
                                {
                                    //connection can be lost while executing command due to a restart
                                }
                                finally
                                {
                                    dynamic.SetPropertyValue(PROPERTY_IS_WORKING, false);
                                }
                            }
                        }
                    }).ToList();

                    await Task.WhenAll(TASKS);
                }
                catch
                {
                }
                finally
                {

                }
            }
        }

        #endregion
    }

    public enum DFAction
    {
        [Localization.Localized("ACTION_FREEZE")]
        Freeze,
        [Localization.Localized("ACTION_THAW")]
        Thaw,
        [Localization.Localized("ACTION_THAW_LOCKED")]
        ThawLocked,
    }

    public enum DFCCommandResult
    {
        /// <summary>
        /// Ok
        /// </summary>
        Ok = 0,
        /// <summary>
        /// Sucess
        /// </summary>
        Sucess = 1,
        /// <summary>
        /// ERROR - User does not have administrator rights
        /// </summary>
        NoRights = 2,
        /// <summary>
        /// ERROR - DFC command not valid on this installation
        /// </summary>
        CommandNotValidForInstall = 3,
        /// <summary>
        /// ERROR - Invalid command
        /// </summary>
        CommandNotValid = 4,
        /// <summary>
        /// ERROR - Internal error executing command
        /// </summary>
        InternalError = 5,
    }

    public enum DFCCommand
    {
        BOOTTHAWED,
        THAWNEXTBOOT,
        ISFROZEN,
        BOOTFROZEN,
        LOCK,
        UNLOCK,
        FREEZENEXTBOOT,
        BOOTTHAWEDNOINPUT
    }
}
