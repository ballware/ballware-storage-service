using AutoMapper;

namespace Ballware.Storage.Data.Ef.Internal;

class StorageMappingProfile : Profile
{
    public StorageMappingProfile()
    {
        CreateMap<Public.Attachment, Persistables.Attachment>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Uuid, opt => opt.MapFrom(src => src.Id));

        CreateMap<Persistables.Attachment, Public.Attachment>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Uuid));

        CreateMap<Public.Temporary, Persistables.Temporary>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Uuid, opt => opt.MapFrom(src => src.Id));

        CreateMap<Persistables.Temporary, Public.Temporary>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Uuid));
    }
}