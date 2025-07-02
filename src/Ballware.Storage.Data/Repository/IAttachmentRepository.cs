namespace Ballware.Storage.Data.Repository;

public interface IAttachmentRepository : ITenantableRepository<Public.Attachment>
{
    Task<IEnumerable<Public.Attachment>> GetAllByEntityAndOwnerId(Guid tenantId, string entity, Guid ownerId);
}