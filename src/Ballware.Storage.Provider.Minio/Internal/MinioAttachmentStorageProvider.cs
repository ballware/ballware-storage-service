using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.Minio.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Ballware.Storage.Provider.Minio.Internal;

class MinioAttachmentStorageProvider : IAttachmentStorageProvider
{
    private IMinioClient Client { get; }
    private MinioStorageOptions Options { get; }
    
    private async Task EnsureBucketExistsAsync()
    {
        var exists = await Client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Options.BucketName));
        
        if (!exists)
            await Client.MakeBucketAsync(new MakeBucketArgs().WithBucket(Options.BucketName));
    }
    
    private async Task<bool> FileExistsAsync(string path)
    {
        try
        {
            await Client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(Options.BucketName)
                .WithObject(path));
            
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
    }
    
    private static string GenerateBlobPath(Guid tenantId, string entity, Guid ownerId, string fileName)
    {
        return $"{tenantId}/{entity}/{ownerId}/{fileName}";
    }
    
    public MinioAttachmentStorageProvider(IMinioClient client, MinioStorageOptions options)
    {
        Client = client;
        Options = options;
    }
    
    public async Task<string> UploadForEntityAndOwnerAsync(Guid tenantId, string entity, Guid ownerId, string fileName, string contentType, Stream data)
    {
        await EnsureBucketExistsAsync();

        var ms = new MemoryStream();
        
        await data.CopyToAsync(ms);
        ms.Position = 0;
        
        var blobPath = GenerateBlobPath(tenantId, entity, ownerId, fileName);
        
        await Client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(Options.BucketName)
            .WithObject(blobPath)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length)
            .WithContentType(contentType)
        );
        
        return blobPath;
    }

    public async Task<Stream?> DownloadForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path)
    {
        await EnsureBucketExistsAsync();
        
        var exists = await FileExistsAsync(path);
        
        if (exists)
        {
            var ms = new MemoryStream();
            
            await Client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(Options.BucketName)
                .WithObject(path)
                .WithCallbackStream(stream => stream.CopyTo(ms))
            );
            
            ms.Position = 0;

            return ms;
        }

        return null;
    }

    public async Task DropForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path)
    {
        await EnsureBucketExistsAsync();
        
        var exists = await FileExistsAsync(path);
        
        if (exists)
        {
            await Client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(Options.BucketName)
                .WithObject(path));
        }
    }
}