using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.Minio.Configuration;
using Ballware.Storage.Provider.Minio.Internal;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Ballware.Storage.Provider.Minio;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareMinioBlobStorage(this IServiceCollection services, MinioStorageOptions options)
    {
        services.AddSingleton(options);

        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.UseSSL)
            .Build());
        
        services.AddScoped<IAttachmentStorageProvider, MinioAttachmentStorageProvider>();
        services.AddScoped<ITemporaryStorageProvider, MinioTemporaryStorageProvider>();
        
        return services;
    }
}