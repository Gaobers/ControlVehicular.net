
using ControlVehicular.Services.Integraciones;
using System.Net;
using System.Text;
using Xunit;

namespace ControlVehicular.TestxUnit.Services;

public class JavaApiServiceTests
{
    // ==========================================
    // SIMULAR SERVIDOR HTTP DE JAVA
    // ==========================================

    private sealed class HttpHandlerFalso : HttpMessageHandler
    {
        public HttpStatusCode CodigoRespuesta { get; set; }
            = HttpStatusCode.OK;

        public string CuerpoRespuesta { get; set; } = "{}";

        public Exception? Excepcion { get; set; }

        public int Solicitudes { get; private set; }

        public string? Ruta { get; private set; }

        public HttpMethod? Metodo { get; private set; }

        public string? EsquemaAutenticacion { get; private set; }

        public string? TokenRecibido { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage solicitud,
            CancellationToken cancellationToken)
        {
            Solicitudes++;

            Ruta = solicitud.RequestUri?.AbsolutePath;
            Metodo = solicitud.Method;

            EsquemaAutenticacion =
                solicitud.Headers.Authorization?.Scheme;

            TokenRecibido =
                solicitud.Headers.Authorization?.Parameter;

            if (Excepcion != null)
            {
                return Task.FromException<HttpResponseMessage>(
                    Excepcion);
            }

            var respuesta = new HttpResponseMessage(CodigoRespuesta)
            {
                Content = new StringContent(
                    CuerpoRespuesta,
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(respuesta);
        }
    }

    private static HttpClient CrearCliente(
        HttpHandlerFalso handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://java.local/")
        };
    }

    // ==========================================
    // 1. CONSULTAR GASTOS CORRECTAMENTE
    // ==========================================

    [Fact]
    public async Task ObtenerGastos_EnviaPeticionCorrectamente()
    {
        var handler = new HttpHandlerFalso
        {
            CuerpoRespuesta = "[{\"id\":1}]"
        };

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        var resultado = await servicio.ObtenerGastosAsync(
            "jwt-prueba");

        Assert.Equal(HttpStatusCode.OK, resultado.EstadoHttp);
        Assert.Equal("[{\"id\":1}]", resultado.Contenido);
        Assert.StartsWith(
            "application/json", resultado.TipoContenido);

        Assert.Equal(HttpMethod.Get, handler.Metodo);
        Assert.Equal("/api/gastos/lista", handler.Ruta);
        Assert.Equal("Bearer", handler.EsquemaAutenticacion);
        Assert.Equal("jwt-prueba", handler.TokenRecibido);
        Assert.Equal(1, handler.Solicitudes);
    }

    // ==========================================
    // 2-3. CONSULTAR VEHICULOS CORRECTAMENTE
    // ==========================================

    [Theory]
    [InlineData(1L)]
    [InlineData(987L)]
    public async Task ObtenerVehiculo_EnviaPeticionCorrectamente(
        long vehiculoId)
    {
        var handler = new HttpHandlerFalso
        {
            CuerpoRespuesta = "{\"id\":1}"
        };

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        var resultado = await servicio.ObtenerVehiculoAsync(
            vehiculoId, "jwt-vehiculo");

        Assert.Equal(HttpStatusCode.OK, resultado.EstadoHttp);
        Assert.Equal("{\"id\":1}", resultado.Contenido);
        Assert.StartsWith(
            "application/json", resultado.TipoContenido);

        Assert.Equal(HttpMethod.Get, handler.Metodo);
        Assert.Equal(
            $"/api/vehiculos/{vehiculoId}", handler.Ruta);

        Assert.Equal("Bearer", handler.EsquemaAutenticacion);
        Assert.Equal("jwt-vehiculo", handler.TokenRecibido);
        Assert.Equal(1, handler.Solicitudes);
    }

    // ==========================================
    // 4-6. TOKEN INVALIDO EN GASTOS
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ObtenerGastos_TokenInvalido_LanzaError(
        string? token)
    {
        var handler = new HttpHandlerFalso();

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.ObtenerGastosAsync(token!));

        // No debe ejecutar solicitudes HTTP.
        Assert.Equal(0, handler.Solicitudes);
    }

    // ==========================================
    // 7-8. IDENTIFICADOR DE VEHICULO INVALIDO
    // ==========================================

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public async Task ObtenerVehiculo_IdInvalido_LanzaError(
        long vehiculoId)
    {
        var handler = new HttpHandlerFalso();

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.ObtenerVehiculoAsync(
                vehiculoId, "jwt"));

        Assert.Equal(0, handler.Solicitudes);
    }

    // ==========================================
    // 9-11. TOKEN INVALIDO EN VEHICULOS
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ObtenerVehiculo_TokenInvalido_LanzaError(
        string? token)
    {
        var handler = new HttpHandlerFalso();

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.ObtenerVehiculoAsync(
                100, token!));

        Assert.Equal(0, handler.Solicitudes);
    }

    // ==========================================
    // 12-13. RESPUESTAS 404 Y 500 EN GASTOS
    // ==========================================

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ObtenerGastos_RetornaEstadoHttp(
        HttpStatusCode estado)
    {
        var handler = new HttpHandlerFalso
        {
            CodigoRespuesta = estado,
            CuerpoRespuesta = "{\"error\":\"Java\"}"
        };

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        var resultado = await servicio.ObtenerGastosAsync("jwt");

        Assert.Equal(estado, resultado.EstadoHttp);
        Assert.Equal(
            "{\"error\":\"Java\"}", resultado.Contenido);

        Assert.Equal(1, handler.Solicitudes);
    }

    // ==========================================
    // 14-15. RESPUESTAS 404 Y 500 EN VEHICULOS
    // ==========================================

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ObtenerVehiculo_RetornaEstadoHttp(
        HttpStatusCode estado)
    {
        var handler = new HttpHandlerFalso
        {
            CodigoRespuesta = estado,
            CuerpoRespuesta = "{\"error\":\"Java\"}"
        };

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        var resultado = await servicio.ObtenerVehiculoAsync(
            100, "jwt");

        Assert.Equal(estado, resultado.EstadoHttp);
        Assert.Equal(
            "{\"error\":\"Java\"}", resultado.Contenido);

        Assert.Equal(1, handler.Solicitudes);
    }

    // ==========================================
    // 16-17. ERRORES DE CONEXION
    // ==========================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JavaApiService_ErrorConexion_PropagaExcepcion(
        bool consultarGastos)
    {
        var handler = new HttpHandlerFalso
        {
            Excepcion = new HttpRequestException(
                "No fue posible conectar con Java.")
        };

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        await Assert.ThrowsAsync<HttpRequestException>(
            async () =>
            {
                if (consultarGastos)
                {
                    await servicio.ObtenerGastosAsync("jwt");
                }
                else
                {
                    await servicio.ObtenerVehiculoAsync(
                        100, "jwt");
                }
            });

        Assert.Equal(1, handler.Solicitudes);
    }

    // ==========================================
    // 18-19. CANCELACION DE SOLICITUDES
    // ==========================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JavaApiService_CancelacionSolicitada_CancelaPeticion(
        bool consultarGastos)
    {
        var handler = new HttpHandlerFalso();

        using var cliente = CrearCliente(handler);
        var servicio = new JavaApiService(cliente);

        using var cancelacion = new CancellationTokenSource();

        // Simulamos una cancelación antes del envío.
        cancelacion.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                if (consultarGastos)
                {
                    await servicio.ObtenerGastosAsync(
                        "jwt",
                        cancelacion.Token);
                }
                else
                {
                    await servicio.ObtenerVehiculoAsync(
                        100,
                        "jwt",
                        cancelacion.Token);
                }
            });
    }
}
