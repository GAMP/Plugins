using GizmoDALV2;
using IntegrationLib;
using ServerService;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicePlugins
{
    #region GizmoServicePluginBase
    [PartNotDiscoverable()]
    [InheritedExport(typeof(IGizmoServicePlugin))]
    public abstract class GizmoServicePluginBase : IGizmoServicePlugin, IPartImportsSatisfiedNotification, IDisposable
    {
        #region CONSTRUCTOR

        /// <summary>
        /// Called on plugin intialization.
        /// </summary>
        public virtual void OnInitialize()
        {
        }

        /// <summary>
        /// Called on plugin deinitialization.
        /// </summary>
        public virtual void OnDeinitialize()
        {
        }

        /// <summary>
        /// When ovveriden should take care of releasing any resources used by plugin.
        /// </summary>
        /// <param name="disposing">Indicate disposing.</param>
        public virtual void OnDisposing(bool disposing)
        {
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the service instance.
        /// </summary>
        [Import(typeof(IGizmoService))]
        protected IGizmoService Service
        {
            get;
            set;
        }

        /// <summary>
        /// Gets repository service instance.
        /// </summary>
        [Import(typeof(IDBRepository))]
        protected IDBRepository RepositoryService
        {
            get;
            set;
        }

        #endregion

        #region IPARTIMPORTSSATISFIEDNOTIFICATION
        /// <summary>
        /// This method is called once all imports are satisfied.
        /// </summary>
        public virtual void OnImportsSatisfied()
        {
        }
        #endregion

        #region IDISPOSABLE
        /// <summary>
        /// Called once object is being disposed.
        /// </summary>
        public void Dispose()
        {
            this.OnDisposing(true);
        }
        #endregion
    }
    #endregion
}
