using AutoMapper;
using Ballware.Shared.Data.Ef.Repository;
using Ballware.Shared.Data.Repository;
using Ballware.Storage.Data.Public;
using Ballware.Storage.Data.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Storage.Data.Ef.Repository;

public class TemporaryBaseRepository : TenantableRepository<Public.Temporary, Persistables.Temporary>, ITemporaryRepository
{
    private IStorageDbContext StorageContext { get; }
    
    public TemporaryBaseRepository(IMapper mapper, IStorageDbContext context, ITenantableRepositoryHook<Temporary, Persistables.Temporary>? hook = null) : base(mapper, context, hook)
    {
        StorageContext = context;
    }

    public async Task<IEnumerable<( Guid TenantId, Temporary Entry )>> AllExpired()
    {
        return await StorageContext.Temporaries.Where(t => t.ExpiryDate <= DateTime.Now)
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(tx => (TenantId: tx.TenantId, Entry: Mapper.Map<Temporary>(tx)) ));
    }
}