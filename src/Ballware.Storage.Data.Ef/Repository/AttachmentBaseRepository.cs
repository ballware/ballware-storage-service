using AutoMapper;
using Ballware.Storage.Data.Public;
using Ballware.Storage.Data.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Storage.Data.Ef.Repository;

public class AttachmentBaseRepository : TenantableBaseRepository<Public.Attachment, Persistables.Attachment>, IAttachmentRepository
{
    public AttachmentBaseRepository(IMapper mapper, IStorageDbContext context, ITenantableRepositoryHook<Public.Attachment, Persistables.Attachment>? hook = null) : base(mapper, context, hook)
    {
    }

    public async Task<IEnumerable<Attachment>> GetAllByEntityAndOwnerId(Guid tenantId, string entity, Guid ownerId)
    {
        return await Context.Attachments.Where(a => a.TenantId == tenantId && a.Entity == entity && a.OwnerId == ownerId)
            .ToListAsync()
            .ContinueWith(t => Mapper.Map<IEnumerable<Attachment>>(t.Result));
    }
}