
using AutoMapper;
using ControlVehicular.DTOs.DocumentoVehiculo;
using ControlVehicular.Models;


namespace ControlVehicular.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // ======================================
            // MODELO -> DTO
            // ======================================
            CreateMap<DocumentoVehiculo, DocumentoVehiculoResponse>();

            // ======================================
            // DTO -> MODELO
            // ======================================

            CreateMap<DocumentoVehiculoRequest, DocumentoVehiculo>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Activo, opt => opt.Ignore())
            .ForMember(dest => dest.FechaRegistro, opt => opt.Ignore())
            .ForMember(dest => dest.FechaActualizacion, opt => opt.Ignore());
        }
    }
}
