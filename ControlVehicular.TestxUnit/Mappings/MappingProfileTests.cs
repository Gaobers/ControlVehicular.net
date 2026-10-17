
using AutoMapper;
using ControlVehicular.DTOs.DocumentoVehiculo;
using ControlVehicular.Mappings;
using ControlVehicular.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ControlVehicular.TestxUnit.Mappings;

public class MappingProfileTests
{
    // Utilizamos la configuración real de AutoMapper.
    private static readonly ServiceProvider Proveedor =
        new ServiceCollection()
            .AddLogging()
            .AddAutoMapper(cfg => { }, typeof(MappingProfile))
            .BuildServiceProvider();


    private static IMapper CrearMapper()
    {
        return Proveedor.GetRequiredService<IMapper>();
    }

    // ==========================================
    // 1. VALIDAR CONFIGURACIÓN
    // ==========================================

    [Fact]
    public void MappingProfile_ConfiguracionEsValida()
    {
        var mapper = CrearMapper();

        mapper.ConfigurationProvider
            .AssertConfigurationIsValid();
    }

    // ==========================================
    // 2. ENTIDAD -> RESPONSE
    // ==========================================

    [Fact]
    public void MappingProfile_EntidadAResponse_CopiaPropiedades()
    {
        var mapper = CrearMapper();

        var entidad = new DocumentoVehiculo
        {
            Id = 10,
            VehiculoId = 100,
            Tipo = "SEGURO",
            Descripcion = "Seguro vehicular",
            NumeroDocumento = "SEG-001",
            EntidadEmisora = "Aseguradora",
            FechaEmision = new DateOnly(2026, 1, 1),
            FechaVencimiento = new DateOnly(2027, 1, 1),
            Activo = true,
            FechaRegistro = new DateTime(2026, 1, 2),
            FechaActualizacion = new DateTime(2026, 2, 3)
        };

        var resultado =
            mapper.Map<DocumentoVehiculoResponse>(entidad);

        Assert.NotNull(resultado);
        Assert.Equal(entidad.Id, resultado.Id);
        Assert.Equal(entidad.VehiculoId, resultado.VehiculoId);
        Assert.Equal(entidad.Tipo, resultado.Tipo);
        Assert.Equal(entidad.Descripcion, resultado.Descripcion);
        Assert.Equal(entidad.NumeroDocumento, resultado.NumeroDocumento);
        Assert.Equal(entidad.EntidadEmisora, resultado.EntidadEmisora);
        Assert.Equal(entidad.FechaEmision, resultado.FechaEmision);
        Assert.Equal(entidad.FechaVencimiento, resultado.FechaVencimiento);
        Assert.Equal(entidad.Activo, resultado.Activo);
        Assert.Equal(entidad.FechaRegistro, resultado.FechaRegistro);
        Assert.Equal(
            entidad.FechaActualizacion,
            resultado.FechaActualizacion);
    }

    // ==========================================
    // 3. REQUEST -> NUEVA ENTIDAD
    // ==========================================

    [Fact]
    public void MappingProfile_RequestAEntidad_CopiaDatos()
    {
        var mapper = CrearMapper();

        var solicitud = new DocumentoVehiculoRequest
        {
            VehiculoId = 200,
            Tipo = "REVISION_TECNICA",
            Descripcion = "Revisión anual",
            NumeroDocumento = "REV-001",
            EntidadEmisora = "Centro de revisión",
            FechaEmision = new DateOnly(2026, 3, 1),
            FechaVencimiento = new DateOnly(2027, 3, 1)
        };

        var entidad =
            mapper.Map<DocumentoVehiculo>(solicitud);

        Assert.NotNull(entidad);
        Assert.Equal(200L, entidad.VehiculoId);
        Assert.Equal("REVISION_TECNICA", entidad.Tipo);
        Assert.Equal("Revisión anual", entidad.Descripcion);
        Assert.Equal("REV-001", entidad.NumeroDocumento);
        Assert.Equal("Centro de revisión", entidad.EntidadEmisora);
        Assert.Equal(solicitud.FechaEmision, entidad.FechaEmision);
        Assert.Equal(
            solicitud.FechaVencimiento,
            entidad.FechaVencimiento);

        // Propiedades no asignadas desde el DTO.
        Assert.Equal(0L, entidad.Id);
        Assert.True(entidad.Activo);
        Assert.Equal(default, entidad.FechaRegistro);
        Assert.Equal(default, entidad.FechaActualizacion);
    }

    // ==========================================
    // 4. ACTUALIZACIÓN SIN MODIFICAR CAMPOS
    // PROTEGIDOS
    // ==========================================

    [Fact]
    public void MappingProfile_ActualizarEntidad_ConservaCamposProtegidos()
    {
        var mapper = CrearMapper();

        var fechaRegistro = new DateTime(2026, 1, 1);
        var fechaActualizacion = new DateTime(2026, 2, 1);

        var entidad = new DocumentoVehiculo
        {
            Id = 50,
            VehiculoId = 100,
            Tipo = "SEGURO",
            Descripcion = "Documento original",
            Activo = false,
            FechaRegistro = fechaRegistro,
            FechaActualizacion = fechaActualizacion
        };

        var solicitud = new DocumentoVehiculoRequest
        {
            VehiculoId = 200,
            Tipo = "OTRO",
            Descripcion = "Documento actualizado"
        };

        var resultado = mapper.Map(solicitud, entidad);

        // Los datos permitidos deben actualizarse.
        Assert.Same(entidad, resultado);
        Assert.Equal(200L, resultado.VehiculoId);
        Assert.Equal("OTRO", resultado.Tipo);
        Assert.Equal("Documento actualizado", resultado.Descripcion);

        // Los campos protegidos deben conservarse.
        Assert.Equal(50L, resultado.Id);
        Assert.False(resultado.Activo);
        Assert.Equal(fechaRegistro, resultado.FechaRegistro);
        Assert.Equal(fechaActualizacion, resultado.FechaActualizacion);
    }

    // ==========================================
    // 5. LIMPIAR CAMPOS OPCIONALES
    // ==========================================

    [Fact]
    public void MappingProfile_CamposOpcionalesNulos_SeActualizan()
    {
        var mapper = CrearMapper();

        var entidad = new DocumentoVehiculo
        {
            Id = 10,
            VehiculoId = 100,
            Tipo = "SEGURO",
            Descripcion = "Descripción anterior",
            NumeroDocumento = "DOC-123",
            EntidadEmisora = "Entidad anterior",
            FechaEmision = new DateOnly(2026, 1, 1),
            FechaVencimiento = new DateOnly(2027, 1, 1)
        };

        var solicitud = new DocumentoVehiculoRequest
        {
            VehiculoId = 100,
            Tipo = "OTRO",
            Descripcion = null,
            NumeroDocumento = null,
            EntidadEmisora = null,
            FechaEmision = null,
            FechaVencimiento = null
        };

        mapper.Map(solicitud, entidad);

        Assert.Null(entidad.Descripcion);
        Assert.Null(entidad.NumeroDocumento);
        Assert.Null(entidad.EntidadEmisora);
        Assert.Null(entidad.FechaEmision);
        Assert.Null(entidad.FechaVencimiento);

        Assert.Equal(10L, entidad.Id);
        Assert.Equal("OTRO", entidad.Tipo);
    }
}
