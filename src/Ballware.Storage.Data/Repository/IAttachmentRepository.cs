namespace Ballware.Storage.Data.Repository;

public interface IAttachmentRepository : ITenantableRepository<Public.Attachment>
{
    Task<IEnumerable<Public.Attachment>> AllByEntityAndOwnerIdAsync(Guid tenantId, string entity, Guid ownerId);
    Task<Public.Attachment?> SingeByEntityOwnerAndFileNameAsync(Guid tenantId, string entity, Guid ownerId, string fileName);
}