using Ballware.Storage.Metadata;
using Ballware.Storage.Provider.Azure.Configuration;
using Ballware.Storage.Provider.Azure.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Provider.Azure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareAzureBlobStorage(this IServiceCollection services, AzureStorageOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<IAttachmentStorageProvider, AzureAttachmentStorageProvider>();
        
        return services;
    }
}