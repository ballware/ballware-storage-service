using AutoMapper;
using Ballware.Storage.Data.Public;
using Ballware.Storage.Data.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Storage.Data.Ef.Repository;

public class TemporaryBaseRepository : TenantableBaseRepository<Public.Temporary, Persistables.Temporary>, ITemporaryRepository
{
    public TemporaryBaseRepository(IMapper mapper, IStorageDbContext context, ITenantableRepositoryHook<Temporary, Persistables.Temporary>? hook = null) : base(mapper, context, hook)
    {
    }

    public async Task<IEnumerable<( Guid TenantId, Temporary Entry )>> AllExpired()
    {
        return await Context.Temporaries.Where(t => t.ExpiryDate <= DateTime.Now)
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(tx => (TenantId: tx.TenantId, Entry: Mapper.Map<Temporary>(t)) ));
    }
}