using Mapster;

namespace Ballware.Storage.Data.Ef.Internal;

class StorageMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Public.Attachment, Persistables.Attachment>()
            .Ignore(dest => dest.Id!)
            .Map(dest => dest.Uuid, src => src.Id);

        config.NewConfig<Persistables.Attachment, Public.Attachment>()
            .Map(dest => dest.Id, src => src.Uuid);

        config.NewConfig<Public.Temporary, Persistables.Temporary>()
            .Ignore(dest => dest.Id!)
            .Map(dest => dest.Uuid, src => src.Id);

        config.NewConfig<Persistables.Temporary, Public.Temporary>()
            .Map(dest => dest.Id, src => src.Uuid);
    }
}
