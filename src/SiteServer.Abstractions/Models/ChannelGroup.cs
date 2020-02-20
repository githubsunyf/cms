using Datory;
using Datory.Annotations;

namespace SiteServer.Abstractions
{
    [DataTable("siteserver_ChannelGroup")]
    public class ChannelGroup : Entity
    {
	    [DataColumn]
        public string GroupName { get; set; }

        [DataColumn]
        public int SiteId { get; set; }

        [DataColumn]
        public int Taxis { get; set; }

        [DataColumn]
        public string Description { get; set; }
	}
}
