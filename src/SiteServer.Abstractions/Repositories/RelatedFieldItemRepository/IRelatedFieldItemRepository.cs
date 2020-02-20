using System.Threading.Tasks;
using Datory;

namespace SiteServer.Abstractions
{
    public partial interface IRelatedFieldItemRepository : IRepository
    {
        Task<int> InsertAsync(RelatedFieldItem info);

        Task<bool> UpdateAsync(RelatedFieldItem info);

        Task DeleteAsync(int siteId, int id);

        Task UpdateTaxisToDownAsync(int siteId, int relatedFieldId, int id, int parentId);

        Task UpdateTaxisToUpAsync(int siteId, int relatedFieldId, int id, int parentId);
    }
}