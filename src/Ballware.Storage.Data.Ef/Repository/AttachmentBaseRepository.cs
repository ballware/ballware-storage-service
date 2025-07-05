using AutoMapper;
using Ballware.Shared.Data.Ef.Repository;
using Ballware.Shared.Data.Repository;
using Ballware.Storage.Data.Public;
using Ballware.Storage.Data.Repository;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Storage.Data.Ef.Repository;

public class AttachmentBaseRepository : TenantableRepository<Public.Attachment, Persistables.Attachment>, IAttachmentRepository
{
    private IStorageDbContext StorageContext { get; } 
    
    public AttachmentBaseRepository(IMapper mapper, IStorageDbContext context, ITenantableRepositoryHook<Public.Attachment, Persistables.Attachment>? hook = null) : base(mapper, context, hook)
    {
        StorageContext = context;
    }

    public async Task<IEnumerable<Attachment>> AllByEntityAndOwnerIdAsync(Guid tenantId, string entity, Guid ownerId)
    {
        return await StorageContext.Attachments.Where(a => a.TenantId == tenantId && a.Entity == entity && a.OwnerId == ownerId)
            .ToListAsync()
            .ContinueWith(t => Mapper.Map<IEnumerable<Attachment>>(t.Result));
    }

    public async Task<Attachment?> SingeByEntityOwnerAndFileNameAsync(Guid tenantId, string entity, Guid ownerId, string fileName)
    {
        return await StorageContext.Attachments
            .Where(a => a.TenantId == tenantId && a.Entity == entity && a.OwnerId == ownerId && a.FileName == fileName)
            .FirstOrDefaultAsync()
            .ContinueWith(t => Mapper.Map<Attachment>(t.Result));
    }
}