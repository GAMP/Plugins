using IntegrationLib;
using NetLib;
using ServerService;
using SharedLib.Dispatcher;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace ServerPlugins
{
    [PluginMetadata("Generic authentication plugin","0.0.0.0")]
    [Export()]
    public class BasicAuthPlugin : ServiceAuthPluginBase
    {
        public override AuthResult Authenticate(IDictionary<string, object> authHeaders, IMessageDispatcher dispatcher)
        {
            //RETURN NULL IN CASE YOU DONT WANT TO HANDLE AUTHENTICATION
            return null;
        }

        public override void PostAuthenticate(AuthResult result,IMessageDispatcher dispatcher)
        {
            //HERE YOU CAN GET THE HOST THAT AUTHENTICATION IS REQUESTED FROM
            //NOTE THAT HOST NUMBER AND HOST ID IS DIFFERENT THINGS
            //HOST ID IS DATABASE ID AND HOST NUMBER IS THE ACTUALL NUMBER ASSIGNED TO THE HOST
            IHostEntry host = this.Service.HostGet(dispatcher);
            //YOU CAN ALSO GET OTHER HOST INFORMATION LIKE MAC ADDRESS IF HOST IS REGISTERED ETC
            if (result.Result == LoginResult.Sucess)
            {
                //AUTH SUCESSFULL               
            }
            else
            {
                //AUTH FAILED
            }  
        }
    }
}
