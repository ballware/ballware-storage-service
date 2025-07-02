using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.Azure.Configuration;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Ballware.Storage.Provider.Azure.Internal;

class AzureAttachmentStorageProvider : IAttachmentStorageProvider
{
    private AzureStorageOptions Options { get; }
    
    private async Task<BlobContainerClient> GetBlobContainerClientAsync()
    {
        var blobContainerClient = new BlobContainerClient(Options.ConnectionString, Options.ContainerName);
        
        await blobContainerClient.CreateIfNotExistsAsync();

        return blobContainerClient;
    }
    
    private static string GenerateBlobPath(Guid tenantId, string entity, Guid ownerId, string fileName)
    {
        return $"{tenantId}/{entity}/{ownerId}/{fileName}";
    }
    
    public AzureAttachmentStorageProvider(AzureStorageOptions options)
    {
        Options = options;
    }
    
    public async Task<string> UploadForEntityAndOwnerAsync(Guid tenantId, string entity, Guid ownerId, string fileName, string contentType, Stream data)
    {
        var blobContainerClient = await GetBlobContainerClientAsync();
        var blobPath = GenerateBlobPath(tenantId, entity, ownerId, fileName);
        
        var blobClient = blobContainerClient.GetBlobClient(blobPath);

        await blobClient.UploadAsync(data, new BlobUploadOptions()
        {
            HttpHeaders = new BlobHttpHeaders()
            {
                ContentType = contentType // Set appropriate content type
            }
        });

        return blobPath;
    }

    public async Task<Stream?> DownloadForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path)
    {
        var blobContainerClient = await GetBlobContainerClientAsync();
        
        var blobClient = blobContainerClient.GetBlobClient(path);
        
        if (await blobClient.ExistsAsync())
        {
            var downloadInfo = await blobClient.DownloadAsync();
            
            return downloadInfo.Value.Content;
        }

        return null;
    }

    public async Task DropForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path)
    {
        var blobContainerClient = await GetBlobContainerClientAsync();
        
        var blobClient = blobContainerClient.GetBlobClient(path);
        
        if (await blobClient.ExistsAsync())
        {
            await blobClient.DeleteAsync();
        }
    }
}