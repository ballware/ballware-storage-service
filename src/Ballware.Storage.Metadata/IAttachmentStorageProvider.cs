namespace Ballware.Storage.Metadata;

public interface IAttachmentStorageProvider
{
    Task<string> UploadForEntityAndOwnerAsync(Guid tenantId, string entity, Guid ownerId, string fileName, string contentType, Stream data);
    Task<Stream?> DownloadForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path);
    Task DropForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path);
}