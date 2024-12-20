using Ballware.Storage.Azure.Internal;
using Ballware.Storage.Provider;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Azure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareAzureFileStorageShare(this IServiceCollection services,
        string connectionString, string shareName)
    {
        services.AddSingleton<IFileStorage>(new AzureFileStorage(connectionString, shareName));

        return services;
    }
}