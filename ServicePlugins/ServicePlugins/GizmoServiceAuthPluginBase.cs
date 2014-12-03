using IntegrationLib;
using SharedLib.Dispatcher;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicePlugins
{
    #region GizmoServiceAuthPluginBase
    /// <summary>
    /// Base class for authentication plugins. 
    /// </summary>
    [PartNotDiscoverable()]
    [InheritedExport(typeof(IGizmoServiceAuthenticationPlugin))]
    public abstract class GizmoServiceAuthPluginBase : GizmoServicePluginBase, IGizmoServiceAuthenticationPlugin
    {
        #region CONSTRUCTOR
        public virtual AuthResult Authenticate(IDictionary<string, object> authHeaders, IMessageDispatcher dispatcher)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Post authenticate procedure.
        /// </summary>
        /// <param name="result">Authentication result.</param>
        /// <remarks>Here you can modify the final result that will be returned to authenticated party.</remarks>
        public virtual void PostAuthenticate(AuthResult result, IMessageDispatcher dispatcher)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
    #endregion
}
