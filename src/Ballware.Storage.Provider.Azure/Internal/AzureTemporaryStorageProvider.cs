using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.Azure.Configuration;

namespace Ballware.Storage.Provider.Azure.Internal;

class AzureTemporaryStorageProvider : ITemporaryStorageProvider
{
    private AzureStorageOptions Options { get; }
    
    private async Task<BlobContainerClient> GetBlobContainerClientAsync()
    {
        var blobContainerClient = new BlobContainerClient(Options.ConnectionString, Options.ContainerName);
        
        await blobContainerClient.CreateIfNotExistsAsync();

        return blobContainerClient;
    }
    
    private static string GenerateBlobPath(Guid tenantId, Guid temporaryId, string fileName)
    {
        return $"{tenantId}/temporary/{temporaryId}/{fileName}";
    }
    
    public AzureTemporaryStorageProvider(AzureStorageOptions options)
    {
        Options = options;
    }
    
    public async Task<Stream?> DownloadByPathAsync(Guid tenantId, string path)
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

    public async Task<string> UploadForIdAsync(Guid tenantId, Guid id, string fileName, string contentType, Stream data)
    {
        var blobContainerClient = await GetBlobContainerClientAsync();
        var blobPath = GenerateBlobPath(tenantId, id, fileName);
        
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

    public async Task DropByPathAsync(Guid tenantId, string path)
    {
        var blobContainerClient = await GetBlobContainerClientAsync();
        
        var blobClient = blobContainerClient.GetBlobClient(path);
        
        if (await blobClient.ExistsAsync())
        {
            await blobClient.DeleteAsync();
        }
    }
}