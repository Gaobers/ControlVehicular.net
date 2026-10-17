
using ControlVehicular.DTOs.DocumentoVehiculo;
using ControlVehicular.Endpoints;
using ControlVehicular.Services.DocumentosVehiculo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace ControlVehicular.TestxUnit.Endpoints;

public class DocumentoVehiculoEndpointsTests
{
    private const string Ruta = "/api/documentos-vehiculos/";

    // ==========================================
    // ENTORNO HTTP DE PRUEBAS
    // ==========================================

    private sealed class Entorno : IAsyncDisposable
    {
        public WebApplication App { get; }
        public HttpClient Client { get; }
        public Mock<IDocumentoVehiculoService> Servicio { get; }

        public Entorno(
            WebApplication app,
            HttpClient client,
            Mock<IDocumentoVehiculoService> servicio)
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

        // Servidor HTTP en memoria.
        builder.WebHost.UseTestServer();

        // Autenticación simulada para las pruebas.
        builder.Services
            .AddAuthentication("Pruebas")
            .AddScheme<AuthenticationSchemeOptions,
                AutenticacionPruebaHandler>(
                "Pruebas", _ => { });

        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();

        var servicio = new Mock<IDocumentoVehiculoService>();

        builder.Services.AddSingleton<IDocumentoVehiculoService>(
            servicio.Object);

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        // Registramos los endpoints reales.
        app.AddDocumentoVehiculoEndpoints();

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

    private static DocumentoVehiculoRequest SolicitudValida()
    {
        return new DocumentoVehiculoRequest
        {
            VehiculoId = 100,
            Tipo = "SEGURO",
            Descripcion = "Seguro vehicular"
        };
    }

    // ==========================================
    // 1-2. OBTENER DOCUMENTOS CON FILTROS
    // ==========================================

    [Theory]
    [InlineData(
        "/api/documentos-vehiculos/", null, false)]
    [InlineData(
        "/api/documentos-vehiculos/?vehiculoId=100&incluirInactivos=true",
        100L, true)]
    public async Task GetDocumentos_DevuelveOk(
        string ruta, long? vehiculoId, bool incluirInactivos)
    {
        await using var entorno = await CrearEntornoAsync();

        entorno.Servicio
            .Setup(s => s.GetDocumentos(
                vehiculoId, incluirInactivos))
            .ReturnsAsync(new List<DocumentoVehiculoResponse>
            {
                new()
                {
                    Id = 10,
                    VehiculoId = 100,
                    Tipo = "SEGURO",
                    Activo = true
                }
            });

        var respuesta = await entorno.Client.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var documentos = await respuesta.Content
            .ReadFromJsonAsync<List<DocumentoVehiculoResponse>>();

        Assert.NotNull(documentos);
        Assert.Single(documentos!);

        entorno.Servicio.Verify(
            s => s.GetDocumentos(
                vehiculoId, incluirInactivos),
            Times.Once);
    }

    // ==========================================
    // 3-4. FILTRO DE VEHICULO INVALIDO
    // ==========================================

    [Theory]
    [InlineData(0L)]
    [InlineData(-10L)]
    public async Task GetDocumentos_IdInvalido_Retorna400(
        long vehiculoId)
    {
        await using var entorno = await CrearEntornoAsync();

        var respuesta = await entorno.Client.GetAsync(
            $"{Ruta}?vehiculoId={vehiculoId}");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetDocumentos(
                It.IsAny<long?>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    // ==========================================
    // 5-6. CONSULTAR DOCUMENTO POR ID
    // ==========================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetDocumento_DevuelveEstadoEsperado(
        bool existe)
    {
        await using var entorno = await CrearEntornoAsync();

        DocumentoVehiculoResponse? documento = existe
            ? new DocumentoVehiculoResponse
            {
                Id = 10,
                VehiculoId = 100,
                Tipo = "SEGURO"
            }
            : null;

        entorno.Servicio
            .Setup(s => s.GetDocumento(10))
            .ReturnsAsync(documento);

        var respuesta = await entorno.Client.GetAsync(
            $"{Ruta}10");

        Assert.Equal(
            existe ? HttpStatusCode.OK : HttpStatusCode.NotFound,
            respuesta.StatusCode);

        if (existe)
        {
            var resultado = await respuesta.Content
                .ReadFromJsonAsync<DocumentoVehiculoResponse>();

            Assert.NotNull(resultado);
            Assert.Equal(10L, resultado!.Id);
        }

        entorno.Servicio.Verify(
            s => s.GetDocumento(10),
            Times.Once);
    }

    // ==========================================
    // 7. CREAR DOCUMENTO CORRECTAMENTE
    // ==========================================

    [Fact]
    public async Task PostDocumento_DatosValidos_Retorna201()
    {
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.PostDocumento(
                It.IsAny<DocumentoVehiculoRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(15L);

        var respuesta = await entorno.Client.PostAsJsonAsync(
            Ruta, SolicitudValida());

        Assert.Equal(
            HttpStatusCode.Created,
            respuesta.StatusCode);

        Assert.Equal(
            "/api/documentos-vehiculos/15",
            respuesta.Headers.Location?.ToString());

        entorno.Servicio.Verify(
            s => s.PostDocumento(
                It.Is<DocumentoVehiculoRequest>(
                    d => d.VehiculoId == 100 &&
                         d.Tipo == "SEGURO"),
                "jwt-prueba",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ==========================================
    // 8. CREAR SIN TOKEN JWT
    // ==========================================

    [Fact]
    public async Task PostDocumento_SinToken_Retorna401()
    {
        await using var entorno = await CrearEntornoAsync();

        var respuesta = await entorno.Client.PostAsJsonAsync(
            Ruta, SolicitudValida());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.PostDocumento(
                It.IsAny<DocumentoVehiculoRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ==========================================
    // 9-10. ACTUALIZAR DOCUMENTO
    // ==========================================

    [Theory]
    [InlineData(1, HttpStatusCode.OK)]
    [InlineData(-1, HttpStatusCode.NotFound)]
    public async Task PutDocumento_RetornaEstadoEsperado(
        int resultadoServicio,
        HttpStatusCode estadoEsperado)
    {
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.PutDocumento(
                10,
                It.IsAny<DocumentoVehiculoRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultadoServicio);

        var respuesta = await entorno.Client.PutAsJsonAsync(
            $"{Ruta}10", SolicitudValida());

        Assert.Equal(estadoEsperado, respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.PutDocumento(
                10,
                It.IsAny<DocumentoVehiculoRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ==========================================
    // 11-12. DESACTIVAR DOCUMENTO
    // ==========================================

    [Theory]
    [InlineData(1, HttpStatusCode.OK)]
    [InlineData(-1, HttpStatusCode.NotFound)]
    public async Task DeleteDocumento_RetornaEstadoEsperado(
        int resultadoServicio,
        HttpStatusCode estadoEsperado)
    {
        await using var entorno = await CrearEntornoAsync();

        entorno.Servicio
            .Setup(s => s.DeleteDocumento(10))
            .ReturnsAsync(resultadoServicio);

        var respuesta = await entorno.Client.DeleteAsync(
            $"{Ruta}10");

        Assert.Equal(estadoEsperado, respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.DeleteDocumento(10),
            Times.Once);
    }

    // ==========================================
    // 13-14. REACTIVAR DOCUMENTO
    // ==========================================

    [Theory]
    [InlineData(1, HttpStatusCode.OK)]
    [InlineData(-1, HttpStatusCode.NotFound)]
    public async Task ActivarDocumento_RetornaEstadoEsperado(
        int resultadoServicio,
        HttpStatusCode estadoEsperado)
    {
        await using var entorno = await CrearEntornoAsync();

        entorno.Servicio
            .Setup(s => s.ActivarDocumento(10))
            .ReturnsAsync(resultadoServicio);

        var respuesta = await entorno.Client.PutAsync(
            $"{Ruta}10/activar", null);

        Assert.Equal(estadoEsperado, respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.ActivarDocumento(10),
            Times.Once);
    }

    // ==========================================
    // 15. USUARIO SIN ROL ADMINISTRADOR
    // ==========================================

    [Fact]
    public async Task Endpoints_SinRolAdministrador_Retorna403()
    {
        await using var entorno = await CrearEntornoAsync();

        entorno.Client.DefaultRequestHeaders.Add(
            "X-Rol-Prueba", "USUARIO");

        var respuesta = await entorno.Client.GetAsync(Ruta);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.GetDocumentos(
                It.IsAny<long?>(),
                It.IsAny<bool>()),
            Times.Never);
    }




    // ==========================================
    // SIMULAR EXCEPCIONES DEL SERVICIO
    // ==========================================

    private static Exception CrearExcepcion(string tipo)
    {
        return tipo switch
        {
            "Argumento" => new ArgumentException(
                "Los datos enviados son inválidos."),

            "NoEncontrado" => new KeyNotFoundException(
                "El vehículo no existe en Java."),

            "Java401" => new HttpRequestException(
                "No autorizado.",
                null,
                HttpStatusCode.Unauthorized),

            "Java403" => new HttpRequestException(
                "Acceso prohibido.",
                null,
                HttpStatusCode.Forbidden),

            "Java500" => new HttpRequestException(
                "Error de comunicación con Java.",
                null,
                HttpStatusCode.InternalServerError),

            "Cancelacion" => new OperationCanceledException(
                "Solicitud cancelada."),

            "BaseDatos" => new DbUpdateException(
                "Error al guardar el documento."),

            _ => throw new ArgumentException(
                "Tipo de excepción no reconocido.")
        };
    }

    // ==========================================
    // 1-7. MANEJO DE EXCEPCIONES EN POST
    // ==========================================

    [Theory]
    [InlineData("Argumento", HttpStatusCode.BadRequest)]
    [InlineData("NoEncontrado", HttpStatusCode.NotFound)]
    [InlineData("Java401", HttpStatusCode.Unauthorized)]
    [InlineData("Java403", HttpStatusCode.Forbidden)]
    [InlineData("Java500", HttpStatusCode.BadGateway)]
    [InlineData("Cancelacion", HttpStatusCode.GatewayTimeout)]
    [InlineData("BaseDatos", HttpStatusCode.Conflict)]
    public async Task PostDocumento_SiServicioFalla_RetornaEstadoCorrecto(
        string tipoExcepcion,
        HttpStatusCode estadoEsperado)
    {
        // ARRANGE
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.PostDocumento(
                It.IsAny<DocumentoVehiculoRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CrearExcepcion(tipoExcepcion));

        // ACT
        var respuesta = await entorno.Client.PostAsJsonAsync(
            Ruta,
            SolicitudValida());

        // ASSERT
        Assert.Equal(
            estadoEsperado,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.PostDocumento(
                It.IsAny<DocumentoVehiculoRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ==========================================
    // 8-14. MANEJO DE EXCEPCIONES EN PUT
    // ==========================================

    [Theory]
    [InlineData("Argumento", HttpStatusCode.BadRequest)]
    [InlineData("NoEncontrado", HttpStatusCode.NotFound)]
    [InlineData("Java401", HttpStatusCode.Unauthorized)]
    [InlineData("Java403", HttpStatusCode.Forbidden)]
    [InlineData("Java500", HttpStatusCode.BadGateway)]
    [InlineData("Cancelacion", HttpStatusCode.GatewayTimeout)]
    [InlineData("BaseDatos", HttpStatusCode.Conflict)]
    public async Task PutDocumento_SiServicioFalla_RetornaEstadoCorrecto(
        string tipoExcepcion,
        HttpStatusCode estadoEsperado)
    {
        // ARRANGE
        await using var entorno = await CrearEntornoAsync();

        AgregarToken(entorno.Client);

        entorno.Servicio
            .Setup(s => s.PutDocumento(
                10,
                It.IsAny<DocumentoVehiculoRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(CrearExcepcion(tipoExcepcion));

        // ACT
        var respuesta = await entorno.Client.PutAsJsonAsync(
            $"{Ruta}10",
            SolicitudValida());

        // ASSERT
        Assert.Equal(
            estadoEsperado,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.PutDocumento(
                10,
                It.IsAny<DocumentoVehiculoRequest>(),
                "jwt-prueba",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ==========================================
    // 15. ACTUALIZAR SIN TOKEN
    // ==========================================

    [Fact]
    public async Task PutDocumento_SinToken_Retorna401()
    {
        // ARRANGE
        await using var entorno = await CrearEntornoAsync();

        // No agregamos Authorization: Bearer.

        // ACT
        var respuesta = await entorno.Client.PutAsJsonAsync(
            $"{Ruta}10",
            SolicitudValida());

        // ASSERT
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);

        entorno.Servicio.Verify(
            s => s.PutDocumento(
                It.IsAny<long>(),
                It.IsAny<DocumentoVehiculoRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

}

// ==========================================
// AUTENTICACION SIMULADA
// ==========================================

public sealed class AutenticacionPruebaHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AutenticacionPruebaHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        var rol = Request.Headers["X-Rol-Prueba"].ToString();

        if (string.IsNullOrWhiteSpace(rol))
        {
            rol = "ADMINISTRADOR";
        }

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier, "usuario-prueba"),
            new Claim(ClaimTypes.Role, rol)
        };

        var identidad = new ClaimsIdentity(
            claims, Scheme.Name);

        var principal = new ClaimsPrincipal(identidad);

        var ticket = new AuthenticationTicket(
            principal, Scheme.Name);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}
