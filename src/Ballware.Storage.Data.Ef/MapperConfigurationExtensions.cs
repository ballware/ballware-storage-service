using AutoMapper;
using Ballware.Storage.Data.Ef.Internal;

namespace Ballware.Storage.Data.Ef;

public static class MapperConfigurationExtensions
{
    public static IMapperConfigurationExpression AddBallwareMetaStorageMappings(
        this IMapperConfigurationExpression configuration)
    {
        configuration.AddProfile<StorageMappingProfile>();

        return configuration;
    }
}