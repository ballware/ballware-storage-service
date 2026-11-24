using Ballware.Storage.Data.Ef.Internal;
using Mapster;

namespace Ballware.Storage.Data.Ef;

public static class MapperConfigurationExtensions
{
    public static TypeAdapterConfig AddBallwareMetaStorageMappings(
        this TypeAdapterConfig configuration)
    {
        new StorageMappingProfile().Register(configuration);

        return configuration;
    }
}
