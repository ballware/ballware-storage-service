using AutoMapper;
using Ballware.Storage.Data.Repository;

namespace Ballware.Storage.Data.Ef.Repository;

public class TemporaryBaseRepository : TenantableBaseRepository<Public.Temporary, Persistables.Temporary>, ITemporaryRepository
{
    public TemporaryBaseRepository(IMapper mapper, IStorageDbContext context, ITenantableRepositoryHook<Public.Temporary, Persistables.Temporary>? hook = null) : base(mapper, context, hook)
    {
    }
}