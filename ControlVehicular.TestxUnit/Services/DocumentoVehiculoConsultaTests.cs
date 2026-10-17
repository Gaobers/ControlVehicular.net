
using AutoMapper;
using ControlVehicular.DTOs.DocumentoVehiculo;
using ControlVehicular.Models;
using ControlVehicular.Services.DocumentosVehiculo;
using ControlVehicular.Services.Integraciones;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ControlVehicular.TestxUnit.Services;

public class DocumentoVehiculoConsultaTests
{
    private static ControlVehicularDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<ControlVehicularDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ControlVehicularDbContext(options);
    }

    // Simula el mapeo de entidades a DTOs.
    private static IMapper CrearMapper()
    {
        var mapper = new Mock<IMapper>();

        mapper.Setup(m => m.Map<DocumentoVehiculoResponse>(
                It.IsAny<object>()))
            .Returns((object origen) =>
                Convertir((DocumentoVehiculo)origen));

        mapper.Setup(m => m.Map<List<DocumentoVehiculoResponse>>(
                It.IsAny<object>()))
            .Returns((object origen) =>
                ((IEnumerable<DocumentoVehiculo>)origen)
                    .Select(Convertir)
                    .ToList());

        return mapper.Object;
    }

    private static DocumentoVehiculoResponse Convertir(
        DocumentoVehiculo documento)
    {
        return new DocumentoVehiculoResponse
        {
            Id = documento.Id,
            VehiculoId = documento.VehiculoId,
            Tipo = documento.Tipo,
            Descripcion = documento.Descripcion,
            Activo = documento.Activo,
            FechaRegistro = documento.FechaRegistro
        };
    }

    private static DocumentoVehiculoService CrearServicio(
        ControlVehicularDbContext db)
    {
        return new DocumentoVehiculoService(
            db,
            CrearMapper(),
            Mock.Of<IJavaApiService>()
        );
    }

    // Datos de ejemplo para las pruebas.
    private static async Task PrepararDatos(
        ControlVehicularDbContext db)
    {
        db.DocumentosVehiculo.AddRange(
            new DocumentoVehiculo
            {
                Id = 1,
                VehiculoId = 100,
                Tipo = "SEGURO",
                Activo = true,
                FechaRegistro = new DateTime(2026, 1, 1)
            },
            new DocumentoVehiculo
            {
                Id = 2,
                VehiculoId = 200,
                Tipo = "OTRO",
                Activo = true,
                FechaRegistro = new DateTime(2026, 1, 3)
            },
            new DocumentoVehiculo
            {
                Id = 3,
                VehiculoId = 100,
                Tipo = "REVISION_TECNICA",
                Activo = false,
                FechaRegistro = new DateTime(2026, 1, 2)
            }
        );

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    // ==========================================
    // PRUEBAS DE GETDOCUMENTO
    // ==========================================

    [Theory]
    [InlineData(1L, true)]
    [InlineData(3L, false)]
    public async Task GetDocumento_SiExiste_DevuelveDocumento(
        long id, bool estadoEsperado)
    {
        using var db = CrearContexto();
        await PrepararDatos(db);

        var servicio = CrearServicio(db);

        var resultado = await servicio.GetDocumento(id);

        Assert.NotNull(resultado);
        Assert.Equal(id, resultado!.Id);
        Assert.Equal(estadoEsperado, resultado.Activo);
    }

    [Fact]
    public async Task GetDocumento_SiNoExiste_RetornaNull()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);

        var resultado = await servicio.GetDocumento(999);

        Assert.Null(resultado);
    }

    // ==========================================
    // PRUEBAS DE GETDOCUMENTOS
    // ==========================================

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public async Task GetDocumentos_IncluirInactivos_FiltraCorrectamente(
        bool incluirInactivos, int cantidadEsperada)
    {
        using var db = CrearContexto();
        await PrepararDatos(db);

        var servicio = CrearServicio(db);

        var resultado = await servicio.GetDocumentos(
            null, incluirInactivos);

        Assert.Equal(cantidadEsperada, resultado.Count);

        if (!incluirInactivos)
        {
            Assert.All(resultado, d => Assert.True(d.Activo));
        }
    }

    [Theory]
    [InlineData(100L, 2)]
    [InlineData(200L, 1)]
    public async Task GetDocumentos_FiltraPorVehiculo(
        long vehiculoId, int cantidadEsperada)
    {
        using var db = CrearContexto();
        await PrepararDatos(db);

        var servicio = CrearServicio(db);

        var resultado = await servicio.GetDocumentos(
            vehiculoId, true);

        Assert.Equal(cantidadEsperada, resultado.Count);
        Assert.All(resultado, d =>
            Assert.Equal(vehiculoId, d.VehiculoId));
    }

    [Fact]
    public async Task GetDocumentos_OrdenaPorFechaDescendente()
    {
        using var db = CrearContexto();
        await PrepararDatos(db);

        var servicio = CrearServicio(db);

        var resultado = await servicio.GetDocumentos(
            null, true);

        var idsEsperados = new long[] { 2, 3, 1 };

        Assert.Equal(
            idsEsperados,
            resultado.Select(d => d.Id));
    }

    [Fact]
    public async Task GetDocumentos_SinCoincidencias_RetornaListaVacia()
    {
        using var db = CrearContexto();
        await PrepararDatos(db);

        var servicio = CrearServicio(db);

        var resultado = await servicio.GetDocumentos(
            999, false);

        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }
}
