using GizmoDALV2;
using IntegrationLib;
using Manager;
using Manager.Modules;
using Manager.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ManagerPlugins
{
    /// <summary>
    /// Simple manager module.
    /// Manager modules created and initialized after creation of all other modules.
    /// </summary>
    [PluginMetadata("Simple module", "0")]
    [Export(typeof(IManagerModule))]
    public class MenuModule : ManagerModulePluginBase
    {
        #region IMPORTS
        /// <summary>
        /// Provides functionality to track current Gizmo service.
        /// </summary>
        [Import()]
        private IServicesManagerService ServiceManager
        {
            get; set;
        }

        /// <summary>
        /// Provides ability to manipulate Gizmo Manager menus.
        /// </summary>
        [Import()]
        private IManagerMenuService MenuService
        {
            get; set;
        }

        /// <summary>
        /// Provides ability to track currently slected user in user module.
        /// </summary>
        [Import()]
        private IUserModuleSelectionProvider UserSelectionLocator
        {
            get; set;
        } 

        [Import()]
        private IManagerMainModule Main
        {
            get; set;
        }

        [Import()]
        private Manager.Services.ICompositionService Comp
        {
            get; set;
        }

        #endregion
          
        public override void Initialize()
        {
            //assign 
            //create our menu in users root menu
            var mainMenu = this.MenuService.CreateMenu("Users", "MYKEY-DISCONNECT", "Disconnect", false, null, OnCanExecuteAction, OnExecuteAction, null);
        }

        private bool OnCanExecuteAction(object param)
        {
            //dissallow execution if no user selected in user module
            return this.UserSelectionLocator.SelectedItem != null;
        }

        private void OnExecuteAction(object param)
        {
        }   
    }
}
