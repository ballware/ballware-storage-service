using Ballware.Shared.Data.Repository;
using Ballware.Storage.Data.Public;

namespace Ballware.Storage.Data.Repository;

public interface ITemporaryRepository : ITenantableRepository<Temporary>
{
    Task<IEnumerable<( Guid TenantId, Temporary Entry )>> AllExpiredAsync();
}