namespace Ballware.Storage.Data.Persistables;

public interface ITenantable
{
    Guid TenantId { get; set; }
}