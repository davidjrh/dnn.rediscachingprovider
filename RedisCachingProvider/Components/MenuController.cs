using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dnn.PersonaBar.Library.Controllers;
using Dnn.PersonaBar.Library.Model;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;

namespace DotNetNuke.Providers.RedisCachingProvider.Components
{
    public class MenuController : IMenuItemController
    {
        public void UpdateParameters(MenuItem menuItem)
        {
        }

        public bool Visible(MenuItem menuItem)
        {
            var user = UserController.Instance.GetCurrentUserInfo();
            return user != null && user.IsSuperUser;
        }

        public IDictionary<string, object> GetSettings(MenuItem menuItem)
        {
            return PortalSettings.Current == null ? null : GetSettings(PortalSettings.Current.PortalId);
        }

        public IDictionary<string, object> GetSettings(int portalId)
        {
#if DEBUG
            var uiUrl = "http://localhost:8080/dist";
#else
            var uiUrl = "./Modules/Dnn.RedisCaching";
#endif
            var apiUrl = "/DesktopModules/Admin/Dnn.PersonaBar/Modules/Dnn.RedisCaching";
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            return new Dictionary<string, object>
            {
                {"uiUrl", uiUrl},
                {"apiUrl", apiUrl},
                {"libraryVersion",  version },
            };
        }

    }
}
