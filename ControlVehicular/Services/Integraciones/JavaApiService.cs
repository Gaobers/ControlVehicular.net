
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
    }
}
