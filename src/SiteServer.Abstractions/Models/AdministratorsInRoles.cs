using Datory;
using Datory.Annotations;

namespace SiteServer.Abstractions
{
    [DataTable("siteserver_AdministratorsInRoles")]
    public class AdministratorsInRoles : Entity
    {
        [DataColumn]
        public string RoleName { get; set; }

        [DataColumn]
        public string UserName { get; set; }
	}
}
