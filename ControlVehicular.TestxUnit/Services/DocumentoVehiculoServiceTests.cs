using AutoMapper;
using ControlVehicular.Models;
using ControlVehicular.Services.DocumentosVehiculo;
using ControlVehicular.Services.Integraciones;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ControlVehicular.TestxUnit.Services;

public class DocumentoVehiculoServiceTests
{
    // Crear una base de datos independiente por prueba.
    private static ControlVehicularDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<ControlVehicularDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ControlVehicularDbContext(options);
    }

    // Crear el servicio con dependencias simuladas.
    private static DocumentoVehiculoService CrearServicio(
        ControlVehicularDbContext db)
    {
        return new DocumentoVehiculoService(
            db,
            Mock.Of<IMapper>(),
            Mock.Of<IJavaApiService>()
        );
    }

    // Preparar un documento para las pruebas.
    private static async Task InsertarDocumento(
        ControlVehicularDbContext db, bool activo)
    {
        var documento = new DocumentoVehiculo
        {
            Id = 10,
            VehiculoId = 100,
            Tipo = "SEGURO",
            Activo = true
        };

        db.DocumentosVehiculo.Add(documento);
        await db.SaveChangesAsync();

        if (!activo)
        {
            documento.Activo = false;
            await db.SaveChangesAsync();
        }

        db.ChangeTracker.Clear();
    }

    // PRUEBAS PARA DESACTIVAR DOCUMENTOS

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task DeleteDocumento_DevuelveResultadoSegunEstado(
        bool activo, int resultadoEsperado)
    {
        using var db = CrearContexto();
        await InsertarDocumento(db, activo);

        var servicio = CrearServicio(db);

        var resultado = await servicio.DeleteDocumento(10);

        Assert.Equal(resultadoEsperado, resultado);

        db.ChangeTracker.Clear();
        var documento = await db.DocumentosVehiculo.FindAsync(10L);

        Assert.NotNull(documento);
        Assert.False(documento!.Activo);
    }

    [Fact]
    public async Task DeleteDocumento_SiNoExiste_RetornaMenosUno()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);

        var resultado = await servicio.DeleteDocumento(999);

        Assert.Equal(-1, resultado);
    }

    // PRUEBAS PARA REACTIVAR DOCUMENTOS

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task ActivarDocumento_DevuelveResultadoSegunEstado(
        bool activo, int resultadoEsperado)
    {
        using var db = CrearContexto();
        await InsertarDocumento(db, activo);

        var servicio = CrearServicio(db);

        var resultado = await servicio.ActivarDocumento(10);

        Assert.Equal(resultadoEsperado, resultado);

        db.ChangeTracker.Clear();
        var documento = await db.DocumentosVehiculo.FindAsync(10L);

        Assert.NotNull(documento);
        Assert.True(documento!.Activo);
    }

    [Fact]
    public async Task ActivarDocumento_SiNoExiste_RetornaMenosUno()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db);

        var resultado = await servicio.ActivarDocumento(999);

        Assert.Equal(-1, resultado);
    }
}
