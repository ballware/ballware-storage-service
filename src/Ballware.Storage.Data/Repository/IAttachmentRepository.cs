using Ballware.Shared.Data.Repository;

namespace Ballware.Storage.Data.Repository;

public interface IAttachmentRepository : ITenantableRepository<Public.Attachment>
{
    Task<IEnumerable<Public.Attachment>> AllAsync(Guid tenantId);
    
    Task<IEnumerable<Public.Attachment>> AllByEntityAsync(Guid tenantId, string entity);
    
    Task<IEnumerable<Public.Attachment>> AllByEntityAndOwnerIdAsync(Guid tenantId, string entity, Guid ownerId);
    
    Task<Public.Attachment?> SingleByEntityOwnerAndFileNameAsync(Guid tenantId, string entity, Guid ownerId, string fileName);
}