
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

public class DocumentoVehiculoPostTests
{
    private static ControlVehicularDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<ControlVehicularDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ControlVehicularDbContext(options);
    }

    private static DocumentoVehiculoRequest SolicitudValida()
    {
        return new DocumentoVehiculoRequest
        {
            VehiculoId = 100,
            Tipo = "SEGURO",
            Descripcion = "Seguro del vehículo",
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

    private static DocumentoVehiculoService CrearServicio(
        ControlVehicularDbContext db,
        Mock<IJavaApiService> java,
        Mock<IMapper>? mapper = null)
    {
        return new DocumentoVehiculoService(
            db,
            (mapper ?? new Mock<IMapper>()).Object,
            java.Object
        );
    }

    // 1. REGISTRO EXITOSO

    [Fact]
    public async Task PostDocumento_DatosValidos_CreaDocumento()
    {
        using var db = CrearContexto();
        var java = CrearJava();
        var mapper = new Mock<IMapper>();

        mapper.Setup(m => m.Map<DocumentoVehiculo>(
                It.IsAny<DocumentoVehiculoRequest>()))
            .Returns((DocumentoVehiculoRequest request) =>
                new DocumentoVehiculo
                {
                    VehiculoId = request.VehiculoId,
                    Tipo = request.Tipo,
                    Descripcion = request.Descripcion
                });

        var servicio = CrearServicio(db, java, mapper);
        var solicitud = SolicitudValida();
        solicitud.Tipo = " seguro ";

        var id = await servicio.PostDocumento(
            solicitud, "token-prueba");

        Assert.True(id > 0);

        var documento = await db.DocumentosVehiculo.SingleAsync();

        Assert.Equal(id, documento.Id);
        Assert.Equal(100L, documento.VehiculoId);
        Assert.Equal("SEGURO", documento.Tipo);
        Assert.True(documento.Activo);

        java.Verify(x => x.ObtenerVehiculoAsync(
            100L, "token-prueba",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // 2. VEHÍCULO INVÁLIDO

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public async Task PostDocumento_VehiculoInvalido_LanzaError(
        long vehiculoId)
    {
        using var db = CrearContexto();
        var java = CrearJava();
        var servicio = CrearServicio(db, java);

        var solicitud = SolicitudValida();
        solicitud.VehiculoId = vehiculoId;

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.PostDocumento(solicitud, "token"));

        java.Verify(x => x.ObtenerVehiculoAsync(
            It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // 3. TIPO DE DOCUMENTO INVÁLIDO

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("LICENCIA")]
    public async Task PostDocumento_TipoInvalido_LanzaError(
        string? tipo)
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db, CrearJava());

        var solicitud = SolicitudValida();
        solicitud.Tipo = tipo!;

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.PostDocumento(solicitud, "token"));
    }

    // 4. LONGITUD DE CAMPOS

    [Theory]
    [InlineData("Descripcion", 151)]
    [InlineData("NumeroDocumento", 101)]
    [InlineData("EntidadEmisora", 151)]
    public async Task PostDocumento_CampoMuyLargo_LanzaError(
        string campo, int longitud)
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db, CrearJava());
        var solicitud = SolicitudValida();

        var texto = new string('A', longitud);

        switch (campo)
        {
            case "Descripcion":
                solicitud.Descripcion = texto;
                break;
            case "NumeroDocumento":
                solicitud.NumeroDocumento = texto;
                break;
            case "EntidadEmisora":
                solicitud.EntidadEmisora = texto;
                break;
        }

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.PostDocumento(solicitud, "token"));
    }

    // 5. FECHAS INCORRECTAS

    [Fact]
    public async Task PostDocumento_FechasInvalidas_LanzaError()
    {
        using var db = CrearContexto();
        var servicio = CrearServicio(db, CrearJava());
        var solicitud = SolicitudValida();

        solicitud.FechaEmision = new DateOnly(2026, 10, 20);
        solicitud.FechaVencimiento = new DateOnly(2026, 10, 10);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.PostDocumento(solicitud, "token"));
    }

    // 6. VEHÍCULO NO ENCONTRADO EN JAVA

    [Fact]
    public async Task PostDocumento_VehiculoNoExiste_LanzaError()
    {
        using var db = CrearContexto();
        var java = CrearJava(HttpStatusCode.NotFound);
        var servicio = CrearServicio(db, java);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => servicio.PostDocumento(
                SolicitudValida(), "token"));

        Assert.Empty(db.DocumentosVehiculo);
    }

    // 7. ERROR EN API JAVA

    [Fact]
    public async Task PostDocumento_ErrorJava_LanzaHttpException()
    {
        using var db = CrearContexto();
        var java = CrearJava(HttpStatusCode.InternalServerError);
        var servicio = CrearServicio(db, java);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => servicio.PostDocumento(
                SolicitudValida(), "token"));

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            exception.StatusCode);

        Assert.Empty(db.DocumentosVehiculo);
    }
}
