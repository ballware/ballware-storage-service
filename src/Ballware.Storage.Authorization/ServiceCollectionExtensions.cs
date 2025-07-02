using Ballware.Storage.Authorization.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Storage.Authorization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareStorageAuthorizationUtils(this IServiceCollection services, string tenantClaim, string userIdClaim, string rightClaim)
    {
        services.AddSingleton<IPrincipalUtils>(new DefaultPrincipalUtils(tenantClaim, userIdClaim, rightClaim));

        return services;
    }
}