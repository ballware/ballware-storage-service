using Amazon.Runtime;
using Amazon.S3;
using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.S3.Configuration;
using Ballware.Storage.Provider.S3.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ballware.Storage.Provider.S3;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareS3BlobStorage(this IServiceCollection services, S3StorageOptions options)
    {
        if (options.Endpoint.Contains("://", StringComparison.Ordinal))
        {
            throw new OptionsValidationException(nameof(S3StorageOptions.Endpoint), typeof(S3StorageOptions), new[]
            {
                $"{nameof(S3StorageOptions)}.{nameof(S3StorageOptions.Endpoint)} must be host:port without a URI scheme; the scheme is derived from {nameof(S3StorageOptions.UseSSL)}."
            });
        }

        services.AddSingleton(options);

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = (options.UseSSL ? "https://" : "http://") + options.Endpoint,
                AuthenticationRegion = options.Region,
                ForcePathStyle = options.ForcePathStyle,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                MaxErrorRetry = 3
            }));

        services.AddSingleton<S3BucketProvisioner>();

        services.AddScoped<IAttachmentStorageProvider, S3AttachmentStorageProvider>();
        services.AddScoped<ITemporaryStorageProvider, S3TemporaryStorageProvider>();

        return services;
    }
}
