using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Ballware.Storage.Provider.S3.Configuration;

namespace Ballware.Storage.Provider.S3.Internal;

// Ensures the configured bucket exists exactly once per process (HeadBucket, then PutBucket on 404).
// Success is cached; failures are not cached and are retried by the next call.
internal class S3BucketProvisioner
{
    private readonly IAmazonS3 _client;
    private readonly S3StorageOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _provisioned;

    public S3BucketProvisioner(IAmazonS3 client, S3StorageOptions options)
    {
        _client = client;
        _options = options;
    }

    public async Task EnsureAsync()
    {
        if (_provisioned)
            return;

        await _gate.WaitAsync();
        try
        {
            if (_provisioned)
                return;

            try
            {
                await _client.HeadBucketAsync(new HeadBucketRequest { BucketName = _options.BucketName });
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                try
                {
                    await _client.PutBucketAsync(new PutBucketRequest
                    {
                        BucketName = _options.BucketName,
                        // no CreateBucketConfiguration body: S3-compatible backends (SeaweedFS, Ceph, …)
                        // commonly reject or misread location constraints
                        UseClientRegion = false
                    });
                }
                catch (AmazonS3Exception inner) when (inner.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
                {
                    // another instance created it concurrently — bucket exists, treat as success
                }
            }

            _provisioned = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
