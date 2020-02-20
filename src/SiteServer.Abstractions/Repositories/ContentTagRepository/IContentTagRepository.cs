using System.Threading.Tasks;
using Datory;

namespace SiteServer.Abstractions
{
    public partial interface IContentTagRepository : IRepository
    {
        Task InsertAsync(int siteId, string tagName);

        Task DeleteAsync(int siteId, string tagName);

        Task DeleteAsync(int siteId);
    }
}