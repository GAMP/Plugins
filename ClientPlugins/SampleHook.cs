using SharedLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationLib
{
    [Export(typeof(IClientHook))]
    public class SampleHook : ClientHookPluginBase
    {        
        public override void OnImportsSatisfied()
        {
            // A common place to attach event handlers is located in this method
            // You should override it in your implementation and hook any desired events provided by this.Client interface property.
            // Any exceptions throwed in event handlers are catched and logged so they should not crash the client.
            this.Client.LoginStateChange += OnLoginStateChange;
            this.Client.ApplicationRated += OnApplicationRated;
            this.Client.UserProfileChange += OnUserProfileChange;

            // One important note for this method is that in case importing party allows recomposition it might be called each time the
            // part is imported so that should be handled correctly.
            // Currently Gizmo client will not recompose the imports so this will be called just once.
        }

        private void OnUserProfileChange(object sender, Client.UserProfileChangeArgs e)
        {
            //Here you can handle the user profile change.
        }

        private void OnApplicationRated(object sender, Client.ApplicationRateEventArgs e)
        {
            var notifyString = String.Format("User {0} rated application {1} with {2}", this.Client.CurrentUser.UserName, e.ApplicationId, e.OverallRating.Value);

            this.Client.NotifyUser(notifyString, "Rated", true);
        }

        private void OnLoginStateChange(object sender, Client.UserEventArgs e)
        {            
            // Here you get login state change event arguments.
            switch(e.State)
            {
                case LoginState.LoginCompleted:

                    if(e.IsUserPasswordRequired)
                        this.Client.NotifyUser("You must update your password!", "Notification", true);
                    
                    if (e.IsUserInfoRequired)
                        this.Client.NotifyUser("You must update your personal information!", "Notification", true);                   
                    
                    break;
                default:
                    break;
            }

            // You can observe the user information by looking at event aruments UserProfile property.
            // One important thing to notice is that LoginState is a flag and in case of LoginCompleted the value is combined with LoggedIn.
            // The correct approach checking the specific login state would be by using HasFlag enum extension.
            if(e.State.HasFlag(LoginState.LoginCompleted))
            {
                this.Client.NotifyUser(string.Format("Welcome back : {0}", e.UserProfile.UserName), "Welcome", true);
            }
        }
    }
}
