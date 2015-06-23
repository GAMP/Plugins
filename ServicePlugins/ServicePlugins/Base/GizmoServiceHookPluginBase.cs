using IntegrationLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServicePlugins
{
    #region GizmoServiceHookPluginBase
    [PartNotDiscoverable()]
    [InheritedExport(typeof(IGizmoServiceHookPlugin))]
    public abstract class GizmoServiceHookPluginBase : GizmoServicePluginBase, IGizmoServiceHookPlugin
    {
    }
    #endregion 
}
