
using ControlVehicular.DTOs.Estadisticas;
using ControlVehicular.Endpoints;
using ControlVehicular.Services.Estadisticas;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ControlVehicular.TestxUnit.Endpoints;

public class EstadisticasGastosEndpointsTests
{
    private const string Ruta = "/api/estadisticas/gastos";

    // ==========================================
    // CONFIGURAR SERVIDOR HTTP DE PRUEBAS
    // ==========================================

    private sealed class Entorno : IAsyncDisposable
    {
        public WebApplication App { get; }
        public HttpClient Client { get; }

        public Mock<IEstadisticasGastosService> Servicio { get; }

        public Entorno(
            WebApplication app,
            HttpClient client,
            Mock<IEstadisticasGastosService> servicio)
        {
            App = app;
            Client = client;
            Servicio = servicio;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }

    private static async Task<Entorno> CrearEntornoAsync()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        // Reutilizamos el handler de autenticación
        // creado para DocumentoVehiculoEndpoints.
        builder.Services
            .AddAuthentication("Pruebas")
            .AddScheme<AuthenticationSchemeOptions,
                AutenticacionPruebaHandler>(
                "Pruebas", _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();

        var servicio = new Mock<IEstadisticasGastosService>();

        builder.Services
            .AddSingleton<IEstadisticasGastosService>(
                servicio.Object);

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // Registrar endpoints reales.
        app.AddEstadisticasGastos();

        await app.StartAsync();

        return new Entorno(
            app,
            app.GetTestClient(),
            servicio);
    }

    private static void AgregarToken(HttpClient cliente)
    {
        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", "jwt-prueba");
    }

    private static EstadisticasGastosResponse RespuestaEjemplo()
    {
        return new EstadisticasGastosResponse
        {
            CantidadGastos = 2,

            TotalesPorMoneda = new List<TotalPorMonedaResponse>
            {
                new()
                {
                    Moneda = "USD",
                    Cantidad = 2,
                    Total = 150m
                }
            }
        };
    }

    // ==========================================
    // 1. CONSULTAR ESTADISTICAS CORRECTAMENTE
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_DatosValidos_Retorna200()
    {
        // ARRANGE
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespuestaEjemplo());

        // ACT
        var respuesta = await entorno.Client.GetAsync(Ruta);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            respuesta.StatusCode);

        var resultado = await respuesta.Content
            .ReadFromJsonAsync<EstadisticasGastosResponse>();

        Assert.NotNull(resultado);
        Assert.Equal(2, resultado!.CantidadGastos);

        var total = Assert.Single(resultado.TotalesPorMoneda);

        Assert.Equal("USD", total.Moneda);
        Assert.Equal(150m, total.Total);

        entorno.Servicio.Verify(
            s => s.GetEstadisticas(
                It.Is<EstadisticasGastosRequest>(f =>
                    f.FechaInicio == null &&
                    f.FechaFin == null &&
                    f.VehiculoId == null),
                "jwt-prueba",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ==========================================
    // 2. CONSULTAR CON FILTROS
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_ConFiltros_EnviaDatosCorrectos()
    {
        // ARRANGE
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(RespuestaEjemplo());

        var ruta = Ruta +
            "?fechaInicio=2026-01-01" +
            "&fechaFin=2026-03-31" +
            "&vehiculoId=100";

        // ACT
        var respuesta = await entorno.Client.GetAsync(ruta);

        // ASSERT
        Assert.Equal(
            HttpStatusCode.OK,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetEstadisticas(
                It.Is<EstadisticasGastosRequest>(f =>
                    f.FechaInicio == new DateOnly(2026, 1, 1) &&
                    f.FechaFin == new DateOnly(2026, 3, 31) &&
                    f.VehiculoId == 100),
                "jwt-prueba",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ==========================================
    // 3. RESPUESTA SIN GASTOS
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_SinGastos_Retorna200()
    {
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstadisticasGastosResponse());

        var respuesta = await entorno.Client.GetAsync(Ruta);

        Assert.Equal(
            HttpStatusCode.OK,
            respuesta.StatusCode);

        var resultado = await respuesta.Content
            .ReadFromJsonAsync<EstadisticasGastosResponse>();

        Assert.NotNull(resultado);
        Assert.Equal(0, resultado!.CantidadGastos);
        Assert.Empty(resultado.TotalesPorMoneda);
        Assert.Empty(resultado.TotalesPorVehiculo);
    }

    // ==========================================
    // 4-5. RANGOS DE FECHAS INVALIDOS
    // ==========================================

    [Theory]
    [InlineData("2026-02-02", "2026-02-01")]
    [InlineData("2026-12-31", "2026-01-01")]
    public async Task GetEstadisticas_FechasInvalidas_Retorna400(
        string inicio,
        string fin)
    {
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        var ruta = Ruta +
            $"?fechaInicio={inicio}&fechaFin={fin}";

        var respuesta = await entorno.Client.GetAsync(ruta);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ==========================================
    // 6-7. VEHICULO INVALIDO
    // ==========================================

    [Theory]
    [InlineData(0L)]
    [InlineData(-10L)]
    public async Task GetEstadisticas_VehiculoInvalido_Retorna400(
        long vehiculoId)
    {
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        var respuesta = await entorno.Client.GetAsync(
            $"{Ruta}?vehiculoId={vehiculoId}");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ==========================================
    // 8-9. CABECERA DE AUTORIZACION INVALIDA
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("Basic abc123")]
    public async Task GetEstadisticas_SinBearer_Retorna401(
        string? authorization)
    {
        await using var entorno = await CrearEntornoAsync();

        if (authorization != null)
        {
            entorno.Client.DefaultRequestHeaders
                .TryAddWithoutValidation(
                    "Authorization", authorization);
        }

        var respuesta = await entorno.Client.GetAsync(Ruta);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ==========================================
    // 10. USUARIO SIN ROL ADMINISTRADOR
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_SinRolAdministrador_Retorna403()
    {
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Client.DefaultRequestHeaders.Add(
            "X-Rol-Prueba", "USUARIO");

        var respuesta = await entorno.Client.GetAsync(Ruta);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ==========================================
    // CREAR EXCEPCIONES SIMULADAS
    // ==========================================

    private static Exception CrearExcepcion(string tipo)
    {
        return tipo switch
        {
            "Argumento" => new ArgumentException(
                "Los filtros son inválidos."),

            "Java401" => new HttpRequestException(
                "No autorizado.",
                null,
                HttpStatusCode.Unauthorized),

            "Java403" => new HttpRequestException(
                "Acceso prohibido.",
                null,
                HttpStatusCode.Forbidden),

            "Java500" => new HttpRequestException(
                "Error al consultar Java.",
                null,
                HttpStatusCode.InternalServerError),

            "Json" => new JsonException(
                "La respuesta de Java es inválida."),

            "Cancelacion" => new OperationCanceledException(
                "La solicitud excedió el tiempo de espera."),

            _ => throw new ArgumentException(
                "Excepción no reconocida.")
        };
    }

    // ==========================================
    // 11-16. MANEJO DE ERRORES DEL SERVICIO
    // ==========================================

    [Theory]
    [InlineData("Argumento", HttpStatusCode.BadRequest)]
    [InlineData("Java401", HttpStatusCode.Unauthorized)]
    [InlineData("Java403", HttpStatusCode.Forbidden)]
    [InlineData("Java500", HttpStatusCode.BadGateway)]
    [InlineData("Json", HttpStatusCode.BadGateway)]
    [InlineData("Cancelacion", HttpStatusCode.GatewayTimeout)]
    public async Task GetEstadisticas_ErrorServicio_RetornaCodigoCorrecto(
        string tipoExcepcion,
        HttpStatusCode estadoEsperado)
    {
        // ARRANGE
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CrearExcepcion(tipoExcepcion));

        // ACT
        var respuesta = await entorno.Client.GetAsync(Ruta);

        // ASSERT
        Assert.Equal(
            estadoEsperado,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetEstadisticas(
                It.IsAny<EstadisticasGastosRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
