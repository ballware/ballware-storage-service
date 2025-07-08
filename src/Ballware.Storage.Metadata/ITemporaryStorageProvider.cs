namespace Ballware.Storage.Metadata;

public interface ITemporaryStorageProvider
{
    Task<Stream?> DownloadByPathAsync(Guid tenantId, string path);
    Task<string> UploadForIdAsync(Guid tenantId, Guid id, string fileName, string contentType, Stream data);

    Task DropByPathAsync(Guid tenantId, string path);
}