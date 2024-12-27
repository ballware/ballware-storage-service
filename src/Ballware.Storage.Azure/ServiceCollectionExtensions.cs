using Ballware.Storage.Azure.Internal;
using Ballware.Storage.Provider;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Azure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareAzureFileStorageShare(this IServiceCollection services,
        string connectionString, string shareName)
    {
        services.AddSingleton<IShareClientFactory>(new DefaultShareClientFactory());
        services.AddSingleton<IFileStorage>(provider => new AzureFileStorage(provider.GetService<IShareClientFactory>() ?? throw new InvalidOperationException("IShareClientFactory needed"), connectionString, shareName));

        return services;
    }
}