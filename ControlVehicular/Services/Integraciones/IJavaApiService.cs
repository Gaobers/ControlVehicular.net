
using System.Net;

namespace ControlVehicular.Services.Integraciones
{
    public record JavaApiResponse(
        HttpStatusCode EstadoHttp,
        string Contenido,
        string? TipoContenido
    );

    public interface IJavaApiService
    {
        Task<JavaApiResponse> ObtenerGastosAsync(
            string token,
            CancellationToken cancellationToken = default
        );

        Task<JavaApiResponse> ObtenerVehiculoAsync(
        long vehiculoId,
        string token,
        CancellationToken cancellationToken = default
);
    }
}
