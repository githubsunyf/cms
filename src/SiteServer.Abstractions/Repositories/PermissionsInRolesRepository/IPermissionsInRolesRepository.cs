using System.Collections.Generic;
using System.Threading.Tasks;
using Datory;

namespace SiteServer.Abstractions
{
    public interface IPermissionsInRolesRepository : IRepository
    {
        Task InsertAsync(PermissionsInRoles pr);

        Task DeleteAsync(string roleName);

        Task<List<string>> GetGeneralPermissionListAsync(IEnumerable<string> roles);
    }
}
