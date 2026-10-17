
using ControlVehicular.DTOs.Estadisticas;
using ControlVehicular.Services.Estadisticas;
using ControlVehicular.Services.Integraciones;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ControlVehicular.TestxUnit.Services;

public class EstadisticasGastosServiceTests
{
    // ==========================================
    // SIMULAR RESPUESTAS DE LA API JAVA
    // ==========================================

    private static Mock<IJavaApiService> CrearJava(
        HttpStatusCode estado,
        string contenido = "[]")
    {
        var java = new Mock<IJavaApiService>();

        java.Setup(x => x.ObtenerGastosAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JavaApiResponse(
                estado,
                contenido,
                "application/json"));

        return java;
    }

    private static EstadisticasGastosService CrearServicio(
        Mock<IJavaApiService> java)
    {
        return new EstadisticasGastosService(java.Object);
    }

    // ==========================================
    // 1. IDENTIFICADOR DE VEHICULO INVALIDO
    // ==========================================

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetEstadisticas_VehiculoInvalido_LanzaError(
        int vehiculoId)
    {
        var java = CrearJava(HttpStatusCode.OK);
        var servicio = CrearServicio(java);

        var filtros = new EstadisticasGastosRequest
        {
            VehiculoId = vehiculoId
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.GetEstadisticas(
                filtros, "token"));

        // No debe consultar Java si los filtros son inválidos.
        java.Verify(x => x.ObtenerGastosAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ==========================================
    // 2. JAVA DEVUELVE 404 (SIN GASTOS)
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_Java404_RetornaEstadisticasVacias()
    {
        var java = CrearJava(
            HttpStatusCode.NotFound, "");

        var servicio = CrearServicio(java);

        var resultado = await servicio.GetEstadisticas(
            new EstadisticasGastosRequest(), "token");

        Assert.NotNull(resultado);
        Assert.Equal(0, resultado.CantidadGastos);
        Assert.Empty(resultado.TotalesPorMoneda);
        Assert.Empty(resultado.TotalesPorVehiculo);
        Assert.Empty(resultado.TotalesPorCategoria);
        Assert.Empty(resultado.TotalesPorMes);
    }

    // ==========================================
    // 3. JAVA DEVUELVE 200 CON LISTA VACIA
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_ListaVacia_RetornaCero()
    {
        var java = CrearJava(
            HttpStatusCode.OK, "[]");

        var servicio = CrearServicio(java);

        var resultado = await servicio.GetEstadisticas(
            new EstadisticasGastosRequest(), "token");

        Assert.Equal(0, resultado.CantidadGastos);
        Assert.Empty(resultado.TotalesPorMoneda);
        Assert.Empty(resultado.TotalesPorVehiculo);
        Assert.Empty(resultado.TotalesPorCategoria);
        Assert.Empty(resultado.TotalesPorMes);
    }

    // ==========================================
    // 4. ERROR DE API JAVA
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_ErrorJava_LanzaHttpException()
    {
        var java = CrearJava(
            HttpStatusCode.InternalServerError);

        var servicio = CrearServicio(java);

        var exception =
            await Assert.ThrowsAsync<HttpRequestException>(
                () => servicio.GetEstadisticas(
                    new EstadisticasGastosRequest(), "token"));

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            exception.StatusCode);
    }

    // ==========================================
    // 5. JSON MALFORMADO
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_JsonInvalido_LanzaError()
    {
        var java = CrearJava(
            HttpStatusCode.OK, "{");

        var servicio = CrearServicio(java);

        await Assert.ThrowsAsync<JsonException>(
            () => servicio.GetEstadisticas(
                new EstadisticasGastosRequest(), "token"));
    }

    // ==========================================
    // 6. JAVA DEVUELVE NULL
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_JsonNull_LanzaError()
    {
        var java = CrearJava(
            HttpStatusCode.OK, "null");

        var servicio = CrearServicio(java);

        await Assert.ThrowsAsync<JsonException>(
            () => servicio.GetEstadisticas(
                new EstadisticasGastosRequest(), "token"));
    }

    // ==========================================
    // 7. DATOS INCOMPLETOS
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_DatosIncompletos_LanzaError()
    {
        var java = CrearJava(
            HttpStatusCode.OK, "[{}]");

        var servicio = CrearServicio(java);

        await Assert.ThrowsAsync<JsonException>(
            () => servicio.GetEstadisticas(
                new EstadisticasGastosRequest(), "token"));
    }

    // ==========================================
    // 8. VERIFICAR ENVIO DEL TOKEN
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_EnviaTokenCorrectamente()
    {
        var java = CrearJava(HttpStatusCode.OK);
        var servicio = CrearServicio(java);

        using var cancellation = new CancellationTokenSource();

        await servicio.GetEstadisticas(
            new EstadisticasGastosRequest(),
            "token-prueba",
            cancellation.Token);

        java.Verify(x => x.ObtenerGastosAsync(
            "token-prueba",
            cancellation.Token), Times.Once);
    }
}
