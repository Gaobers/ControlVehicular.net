
using System.Net.Http.Headers;

namespace ControlVehicular.Services.Integraciones
{
    public class JavaApiService : IJavaApiService
    {
        private readonly HttpClient _httpClient;

        public JavaApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // ==========================================
        // CONSULTAR GASTOS DESDE JAVA
        // ==========================================

        public async Task<JavaApiResponse> ObtenerGastosAsync(
            string token,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException(
                    "El token JWT es obligatorio."
                );
            }

            // Crear petición HTTP
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "api/gastos/lista"
            );

            // Enviar el JWT generado por Java
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token
                );

            // Consumir endpoint de Java
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            // Obtener contenido de respuesta
            var contenido = await response.Content
                .ReadAsStringAsync(cancellationToken);

            var tipoContenido = response.Content
                .Headers.ContentType?.ToString();

            return new JavaApiResponse(
                response.StatusCode,
                contenido,
                tipoContenido
            );
        }


        // ==========================================
        // VERIFICAR VEHICULO EN LA API JAVA
        // ==========================================

        public async Task<JavaApiResponse> ObtenerVehiculoAsync(
            long vehiculoId,
            string token,
            CancellationToken cancellationToken = default
        )
        {
            if (vehiculoId <= 0)
            {
                throw new ArgumentException(
                    "El identificador del vehículo no es válido."
                );
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException(
                    "El token JWT es obligatorio."
                );
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/vehiculos/{vehiculoId}"
            );

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    token
                );

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken
            );

            var contenido = await response.Content
                .ReadAsStringAsync(cancellationToken);

            return new JavaApiResponse(
                response.StatusCode,
                contenido,
                response.Content.Headers.ContentType?.ToString()
            );
        }

    }
}
