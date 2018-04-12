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
    public class ModulePlugin : GizmoServiceModulePluginBase
    {
        public override void Initialize()
        {
            base.Initialize();

            //cast to repo service inteface defined in GizmoDalEntities project (need to reference it) 
            var repoService = this.Service as GizmoDALV2.IDBRepository;

            //you can save your settings like this
            //the setting name is unique in db so choose some non-colliding values
            repoService.SettingSet("CLOSE_TIME", DateTime.Now.ToShortDateString(), "MYPLUGIN");

            //get all active usage sessions
            foreach (var usageSession in repoService.UsageSessionGetActive())
            {
                try
                {
                    //invoice active usage session
                    var invoice = repoService.UsageSessionInvoice(usageSession);

                    //output
                    if (Environment.UserInteractive)
                        Console.WriteLine(string.Format("Invoiced usage session {0}", invoice.Id));

                    repoService.LogAdd(new GizmoDALV2.Entities.Log() { Message = "Oh i need to log this", Category = SharedLib.LogCategories.Generic });
                }
                catch (GizmoDALV2.EntityNotFoundExcpetion)
                {
                    //wrong usage session id
                }
                catch (Exception)
                {
                    //some other error
                    //for example already invoiced
                }
            }
        }
    }
}
