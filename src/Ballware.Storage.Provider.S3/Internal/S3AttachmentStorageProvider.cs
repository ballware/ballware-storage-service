using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.S3.Configuration;

namespace Ballware.Storage.Provider.S3.Internal;

// Compatibility notes for S3-compatible backends:
// - UseChunkEncoding = false is mandatory: AWS SDK v4 defaults to aws-chunked content encoding,
//   which e.g. SeaweedFS does not decode on upload — objects would land corrupted.
// - Object keys must not contain leading slashes, duplicate slashes or ".." segments: SigV4
//   path signing would diverge from what such backends verify (SignatureDoesNotMatch) and the
//   bucket layout could escape its prefix. GenerateBlobPath normalizes the file name accordingly.
// - Uploads stay buffered through MemoryStream: over plain HTTP the SDK signs the payload and
//   requires a seekable stream.
class S3AttachmentStorageProvider : IAttachmentStorageProvider
{
    private IAmazonS3 Client { get; }
    private S3StorageOptions Options { get; }
    private S3BucketProvisioner Provisioner { get; }

    private async Task<bool> FileExistsAsync(string path)
    {
        try
        {
            await Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = Options.BucketName,
                Key = path
            });

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static string GenerateBlobPath(Guid tenantId, string entity, Guid ownerId, string fileName)
    {
        return $"{tenantId}/{entity}/{ownerId}/{NormalizeFileName(fileName)}";
    }

    private static string NormalizeFileName(string fileName)
    {
        var segments = fileName
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0)
            .ToArray();

        if (Array.Exists(segments, segment => segment == ".."))
            throw new ArgumentException("File name must not contain '..' path segments.", nameof(fileName));

        return string.Join('/', segments.Where(segment => segment != "."));
    }

    public S3AttachmentStorageProvider(IAmazonS3 client, S3StorageOptions options, S3BucketProvisioner provisioner)
    {
        Client = client;
        Options = options;
        Provisioner = provisioner;
    }

    public async Task<string> UploadForEntityAndOwnerAsync(Guid tenantId, string entity, Guid ownerId, string fileName, string contentType, Stream data)
    {
        await Provisioner.EnsureAsync();

        var ms = new MemoryStream();

        await data.CopyToAsync(ms);
        ms.Position = 0;

        var blobPath = GenerateBlobPath(tenantId, entity, ownerId, fileName);

        await Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Options.BucketName,
            Key = blobPath,
            InputStream = ms,
            ContentType = contentType,
            UseChunkEncoding = false,
            AutoCloseStream = false
        });

        return blobPath;
    }

    public async Task<Stream?> DownloadForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path)
    {
        await Provisioner.EnsureAsync();

        var exists = await FileExistsAsync(path);

        if (exists)
        {
            var ms = new MemoryStream();

            using var response = await Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = Options.BucketName,
                Key = path
            });

            await response.ResponseStream.CopyToAsync(ms);
            ms.Position = 0;

            return ms;
        }

        return null;
    }

    public async Task DropForEntityAndOwnerByPathAsync(Guid tenantId, string entity, Guid ownerId, string path)
    {
        await Provisioner.EnsureAsync();

        var exists = await FileExistsAsync(path);

        if (exists)
        {
            await Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = Options.BucketName,
                Key = path
            });
        }
    }
}
