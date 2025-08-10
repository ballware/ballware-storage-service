using AutoMapper;
using Ballware.Shared.Data.Ef.Repository;
using Ballware.Shared.Data.Repository;
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

    public async Task<IEnumerable<Public.Attachment>> AllAsync(Guid tenantId)
    {
        return await StorageContext.Attachments.Where(a => a.TenantId == tenantId)
            .ToListAsync()
            .ContinueWith(t => Mapper.Map<IEnumerable<Public.Attachment>>(t.Result));
    }

    public async Task<IEnumerable<Public.Attachment>> AllByEntityAsync(Guid tenantId, string entity)
    {
        return await StorageContext.Attachments.Where(a => a.TenantId == tenantId && a.Entity == entity)
            .ToListAsync()
            .ContinueWith(t => Mapper.Map<IEnumerable<Public.Attachment>>(t.Result));
    }

    public async Task<IEnumerable<Public.Attachment>> AllByEntityAndOwnerIdAsync(Guid tenantId, string entity, Guid ownerId)
    {
        return await StorageContext.Attachments.Where(a => a.TenantId == tenantId && a.Entity == entity && a.OwnerId == ownerId)
            .ToListAsync()
            .ContinueWith(t => Mapper.Map<IEnumerable<Public.Attachment>>(t.Result));
    }

    public async Task<Public.Attachment?> SingleByEntityOwnerAndFileNameAsync(Guid tenantId, string entity, Guid ownerId, string fileName)
    {
        return await StorageContext.Attachments
            .Where(a => a.TenantId == tenantId && a.Entity == entity && a.OwnerId == ownerId && a.FileName == fileName)
            .FirstOrDefaultAsync()
            .ContinueWith(t => Mapper.Map<Public.Attachment>(t.Result));
    }
}