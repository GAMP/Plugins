using IntegrationLib;
using Manager.Modules;
using Manager.Services;
using Manager.ViewModels;
using Manager.Views;
using SharedLib.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ManagerPlugins
{
    /// <summary>
    /// Deep Freeze manager module.
    /// </summary>
    [PluginMetadata("Deep freeze", "1.0")]
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

        #endregion

        #region FIELDS
        //Create unique string keys for your menu items, guids can server a perfect role for that
        private readonly string DF_ROOT_MENU_KEY = "a481bf50-4b91-49db-8181-bddbe17d6ca3";
        private readonly string DF_MENU_FREEZE = "aee3d0a8-52f5-4f77-9754-36b8461f06a9";
        private readonly string DF_MENU_UNFREEZE = "615d1a91-98ce-46f3-88d3-b30522a2459b";
        #endregion

        #region OVERRIDES

        public override void Initialize()
        {
            base.Initialize();

            //create your menus
            var mainMenu = this.MenuService.CreateMenu(Manager.KnownMenuArea.HostManagementModule, DF_ROOT_MENU_KEY, "Deep Freeze", false, "icon-deep-freeze", null, null, null);
            var freezeMenu = this.MenuService.CreateMenu(DF_ROOT_MENU_KEY, DF_MENU_FREEZE,"Freeze", false, "icon-deep-freeze", OnCanExecuteAction, OnExecuteAction, true);
            var unfreezeMenu = this.MenuService.CreateMenu(DF_ROOT_MENU_KEY, DF_MENU_UNFREEZE,"Unfreeze", false, "icon-deep-unfreeze", OnCanExecuteAction, OnExecuteAction, false);

            var column = new DataGridTemplateColumn() { Header = "Deep Freeze" };

            this.HostView.Columns.Last().Width = DataGridLength.Auto;

            this.HostView.Columns.Add(column);

            var resources = new ResourceDictionary();
            resources.Source = new Uri("pack://application:,,,/ManagerPlugins;component/Resources/DeepFreezePluginRes.xaml", UriKind.Absolute);

            column.CellTemplate = resources["_deep_freeze_display_template"] as DataTemplate;

            //add your resources to application
            //this will allow them to be resolved by other application parts
            Application.Current.Resources.MergedDictionaries.Add(resources);

        }

        public override void Start()
        {
            base.Start();

            foreach (var host in this.HostLocator.Get().OfType<System.ComponentModel.IDynamicPropertyObject>())
            {
                var deepFreezeEnabledProperty = host.AddProperty<bool>("DeepFreezeEnabled");
                var isComputerProperty = host.AddProperty<bool>("IsComputerHost");

                bool isComputer = host as IHostComputerViewModel != null;

                isComputerProperty.SetValue(host, isComputer);
            }

        }

        #endregion

        #region COMMANDS

        private bool OnCanExecuteAction(object param)
        {
            return true;
        }
        private void OnExecuteAction(object param)
        { } 

        #endregion
    }
}
