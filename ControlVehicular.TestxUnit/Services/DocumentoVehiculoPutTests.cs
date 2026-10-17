
using AutoMapper;
using ControlVehicular.DTOs.DocumentoVehiculo;
using ControlVehicular.Models;
using ControlVehicular.Services.DocumentosVehiculo;
using ControlVehicular.Services.Integraciones;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Net;
using Xunit;

namespace ControlVehicular.TestxUnit.Services;

public class DocumentoVehiculoPutTests
{
    private static ControlVehicularDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<ControlVehicularDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ControlVehicularDbContext(options);
    }

    private static async Task PrepararDocumento(
        ControlVehicularDbContext db,
        bool activo = true)
    {
        db.DocumentosVehiculo.Add(new DocumentoVehiculo
        {
            Id = 10,
            VehiculoId = 100,
            Tipo = "SEGURO",
            Descripcion = "Documento original",
            Activo = activo
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static DocumentoVehiculoRequest SolicitudValida()
    {
        return new DocumentoVehiculoRequest
        {
            VehiculoId = 100,
            Tipo = "OTRO",
            Descripcion = "Documento actualizado",
            FechaEmision = new DateOnly(2026, 1, 1),
            FechaVencimiento = new DateOnly(2027, 1, 1)
        };
    }

    private static Mock<IJavaApiService> CrearJava(
        HttpStatusCode estado = HttpStatusCode.OK)
    {
        var java = new Mock<IJavaApiService>();

        java.Setup(x => x.ObtenerVehiculoAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JavaApiResponse(
                estado, "{}", "application/json"));

        return java;
    }

    private static Mock<IMapper> CrearMapper()
    {
        var mapper = new Mock<IMapper>();

        mapper.Setup(m => m.Map<
                DocumentoVehiculoRequest,
                DocumentoVehiculo>(
                It.IsAny<DocumentoVehiculoRequest>(),
                It.IsAny<DocumentoVehiculo>()))
            .Returns((
                DocumentoVehiculoRequest origen,
                DocumentoVehiculo destino) =>
            {
                destino.VehiculoId = origen.VehiculoId;
                destino.Tipo = origen.Tipo;
                destino.Descripcion = origen.Descripcion;
                destino.NumeroDocumento = origen.NumeroDocumento;
                destino.EntidadEmisora = origen.EntidadEmisora;
                destino.FechaEmision = origen.FechaEmision;
                destino.FechaVencimiento = origen.FechaVencimiento;

                return destino;
            });

        return mapper;
    }

    private static DocumentoVehiculoService CrearServicio(
        ControlVehicularDbContext db,
        Mock<IJavaApiService> java)
    {
        return new DocumentoVehiculoService(
            db,
            CrearMapper().Object,
            java.Object
        );
    }

    // 1. ACTUALIZACIÓN EXITOSA

    [Fact]
    public async Task PutDocumento_DatosValidos_ActualizaDocumento()
    {
        using var db = CrearContexto();
        await PrepararDocumento(db);

        var java = CrearJava();
        var servicio = CrearServicio(db, java);
        var solicitud = SolicitudValida();

        solicitud.Tipo = " otro ";

        var resultado = await servicio.PutDocumento(
            10, solicitud, "token");

        Assert.Equal(1, resultado);

        db.ChangeTracker.Clear();
        var documento = await db.DocumentosVehiculo.FindAsync(10L);

        Assert.NotNull(documento);
        Assert.Equal("OTRO", documento!.Tipo);
        Assert.Equal("Documento actualizado", documento.Descripcion);
        Assert.Equal(100L, documento.VehiculoId);
        Assert.True(documento.Activo);

        java.Verify(x => x.ObtenerVehiculoAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // 2. CAMBIO DE VEHÍCULO

    [Fact]
    public async Task PutDocumento_CambiaVehiculo_VerificaJava()
    {
        using var db = CrearContexto();
        await PrepararDocumento(db);

        var java = CrearJava();
        var servicio = CrearServicio(db, java);
        var solicitud = SolicitudValida();

        solicitud.VehiculoId = 200;

        var resultado = await servicio.PutDocumento(
            10, solicitud, "token-prueba");

        Assert.Equal(1, resultado);

        java.Verify(x => x.ObtenerVehiculoAsync(
            200L,
            "token-prueba",
            It.IsAny<CancellationToken>()), Times.Once);

        db.ChangeTracker.Clear();
        var documento = await db.DocumentosVehiculo.FindAsync(10L);

        Assert.NotNull(documento);
        Assert.Equal(200L, documento!.VehiculoId);
    }

    // 3. DOCUMENTO NO EXISTENTE

    [Fact]
    public async Task PutDocumento_SiNoExiste_RetornaMenosUno()
    {
        using var db = CrearContexto();
        var java = CrearJava();
        var servicio = CrearServicio(db, java);

        var resultado = await servicio.PutDocumento(
            999, SolicitudValida(), "token");

        Assert.Equal(-1, resultado);

        java.Verify(x => x.ObtenerVehiculoAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // 4. VEHÍCULO INVÁLIDO

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task PutDocumento_VehiculoInvalido_LanzaError(
        long vehiculoId)
    {
        using var db = CrearContexto();
        await PrepararDocumento(db);

        var servicio = CrearServicio(db, CrearJava());
        var solicitud = SolicitudValida();

        solicitud.VehiculoId = vehiculoId;

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.PutDocumento(
                10, solicitud, "token"));
    }

    // 5. TIPO INVÁLIDO

    [Theory]
    [InlineData("")]
    [InlineData("LICENCIA")]
    public async Task PutDocumento_TipoInvalido_LanzaError(
        string tipo)
    {
        using var db = CrearContexto();
        await PrepararDocumento(db);

        var servicio = CrearServicio(db, CrearJava());
        var solicitud = SolicitudValida();

        solicitud.Tipo = tipo;

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.PutDocumento(
                10, solicitud, "token"));
    }

    // 6. FECHAS INVÁLIDAS

    [Fact]
    public async Task PutDocumento_FechasInvalidas_LanzaError()
    {
        using var db = CrearContexto();
        await PrepararDocumento(db);

        var servicio = CrearServicio(db, CrearJava());
        var solicitud = SolicitudValida();

        solicitud.FechaEmision = new DateOnly(2026, 10, 20);
        solicitud.FechaVencimiento = new DateOnly(2026, 10, 10);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.PutDocumento(
                10, solicitud, "token"));
    }

    // 7. VEHÍCULO INEXISTENTE EN JAVA

    [Fact]
    public async Task PutDocumento_VehiculoNoExiste_LanzaError()
    {
        using var db = CrearContexto();
        await PrepararDocumento(db);

        var java = CrearJava(HttpStatusCode.NotFound);
        var servicio = CrearServicio(db, java);
        var solicitud = SolicitudValida();

        solicitud.VehiculoId = 200;

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => servicio.PutDocumento(
                10, solicitud, "token"));

        db.ChangeTracker.Clear();
        var documento = await db.DocumentosVehiculo.FindAsync(10L);

        Assert.NotNull(documento);
        Assert.Equal(100L, documento!.VehiculoId);
    }

    // 8. ERROR DE API JAVA

    [Fact]
    public async Task PutDocumento_ErrorJava_LanzaHttpException()
    {
        using var db = CrearContexto();
        await PrepararDocumento(db);

        var java = CrearJava(HttpStatusCode.InternalServerError);
        var servicio = CrearServicio(db, java);
        var solicitud = SolicitudValida();

        solicitud.VehiculoId = 200;

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => servicio.PutDocumento(
                10, solicitud, "token"));

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            exception.StatusCode);
    }

    // 9. CONSERVAR ESTADO INACTIVO

    [Fact]
    public async Task PutDocumento_DocumentoInactivo_ConservaEstado()
    {
        using var db = CrearContexto();
        await PrepararDocumento(db, activo: false);

        var servicio = CrearServicio(db, CrearJava());

        var resultado = await servicio.PutDocumento(
            10, SolicitudValida(), "token");

        Assert.Equal(1, resultado);

        db.ChangeTracker.Clear();
        var documento = await db.DocumentosVehiculo.FindAsync(10L);

        Assert.NotNull(documento);
        Assert.False(documento!.Activo);
        Assert.Equal("OTRO", documento.Tipo);
    }
}
