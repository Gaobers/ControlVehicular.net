
using System.Net;
using System.Text.Json;
using ControlVehicular.DTOs.Estadisticas;
using ControlVehicular.Services.Integraciones;

namespace ControlVehicular.Services.Estadisticas
{
    public class EstadisticasGastosService : IEstadisticasGastosService
    {
        private readonly IJavaApiService _javaApiService;

        private static readonly JsonSerializerOptions _jsonOptions =
            new(JsonSerializerDefaults.Web);

        public EstadisticasGastosService(
            IJavaApiService javaApiService
        )
        {
            _javaApiService = javaApiService;
        }

        // ==========================================
        // OBTENER ESTADISTICAS DE GASTOS
        // ==========================================

        public async Task<EstadisticasGastosResponse> GetEstadisticas(
            EstadisticasGastosRequest filtros,
            string token,
            CancellationToken cancellationToken = default
        )
        {
            // Validar filtros
            if (filtros.FechaInicio.HasValue &&
                filtros.FechaFin.HasValue &&
                filtros.FechaInicio.Value > filtros.FechaFin.Value)
            {
                throw new ArgumentException(
                    "La fecha de inicio no puede ser posterior a la fecha final."
                );
            }

            if (filtros.VehiculoId.HasValue &&
                filtros.VehiculoId.Value <= 0)
            {
                throw new ArgumentException(
                    "El identificador del vehículo debe ser mayor que cero."
                );
            }

            // ======================================
            // OBTENER GASTOS DESDE JAVA
            // ======================================

            var respuesta = await _javaApiService.ObtenerGastosAsync(
                token,
                cancellationToken
            );

            List<GastoJavaDTO> gastos;

            if (respuesta.EstadoHttp == HttpStatusCode.NotFound)
            {
                // El controlador Java devuelve 404
                // cuando la lista de gastos está vacía.
                gastos = new List<GastoJavaDTO>();
            }
            else if (respuesta.EstadoHttp != HttpStatusCode.OK)
            {
                throw new HttpRequestException(
                    "No fue posible consultar los gastos desde Java.",
                    null,
                    respuesta.EstadoHttp
                );
            }
            else
            {
                gastos = JsonSerializer.Deserialize<List<GastoJavaDTO>>(
                    respuesta.Contenido,
                    _jsonOptions
                ) ?? throw new JsonException(
                    "Java no devolvió una lista de gastos válida."
                );
            }

            // ======================================
            // VALIDAR DATOS RECIBIDOS
            // ======================================

            if (gastos.Any(g =>
                g.Id <= 0 ||
                g.VehiculoId <= 0 ||
                g.CategoriaId <= 0 ||
                string.IsNullOrWhiteSpace(g.Moneda) ||
                g.Fecha == default))
            {
                throw new JsonException(
                    "La respuesta de Java contiene datos incompletos."
                );
            }

            // ======================================
            // FILTRAR GASTOS
            // ======================================

            var gastosFiltrados = gastos
                .Where(g => g.Activo)
                .Where(g =>
                    !filtros.FechaInicio.HasValue ||
                    g.Fecha >= filtros.FechaInicio.Value
                )
                .Where(g =>
                    !filtros.FechaFin.HasValue ||
                    g.Fecha <= filtros.FechaFin.Value
                )
                .Where(g =>
                    !filtros.VehiculoId.HasValue ||
                    g.VehiculoId == filtros.VehiculoId.Value
                )
                .Select(g => new
                {
                    g.VehiculoId,
                    g.CategoriaId,
                    g.Fecha,
                    g.Monto,
                    Moneda = g.Moneda.Trim().ToUpperInvariant()
                })
                .ToList();

            // ======================================
            // GENERAR ESTADISTICAS
            // ======================================

            var resultado = new EstadisticasGastosResponse
            {
                CantidadGastos = gastosFiltrados.Count,

                FechaInicio = filtros.FechaInicio,
                FechaFin = filtros.FechaFin,
                VehiculoId = filtros.VehiculoId,

                // ==================================
                // TOTALES POR MONEDA
                // ==================================

                TotalesPorMoneda = gastosFiltrados
                    .GroupBy(g => g.Moneda)
                    .Select(grupo => new TotalPorMonedaResponse
                    {
                        Moneda = grupo.Key,
                        Cantidad = grupo.Count(),
                        Total = grupo.Sum(g => g.Monto)
                    })
                    .OrderBy(x => x.Moneda)
                    .ToList(),

                // ==================================
                // TOTALES POR VEHICULO
                // ==================================

                TotalesPorVehiculo = gastosFiltrados
                    .GroupBy(g => new
                    {
                        g.VehiculoId,
                        g.Moneda
                    })
                    .Select(grupo => new TotalPorVehiculoResponse
                    {
                        VehiculoId = grupo.Key.VehiculoId,
                        Moneda = grupo.Key.Moneda,
                        Cantidad = grupo.Count(),
                        Total = grupo.Sum(g => g.Monto)
                    })
                    .OrderBy(x => x.VehiculoId)
                    .ToList(),

                // ==================================
                // TOTALES POR CATEGORIA
                // ==================================

                TotalesPorCategoria = gastosFiltrados
                    .GroupBy(g => new
                    {
                        g.CategoriaId,
                        g.Moneda
                    })
                    .Select(grupo => new TotalPorCategoriaResponse
                    {
                        CategoriaId = grupo.Key.CategoriaId,
                        Moneda = grupo.Key.Moneda,
                        Cantidad = grupo.Count(),
                        Total = grupo.Sum(g => g.Monto)
                    })
                    .OrderBy(x => x.CategoriaId)
                    .ToList(),

                // ==================================
                // TOTALES POR MES
                // ==================================

                TotalesPorMes = gastosFiltrados
                    .GroupBy(g => new
                    {
                        Mes = g.Fecha.ToString("yyyy-MM"),
                        g.Moneda
                    })
                    .Select(grupo => new TotalPorMesResponse
                    {
                        Mes = grupo.Key.Mes,
                        Moneda = grupo.Key.Moneda,
                        Cantidad = grupo.Count(),
                        Total = grupo.Sum(g => g.Monto)
                    })
                    .OrderBy(x => x.Mes)
                    .ThenBy(x => x.Moneda)
                    .ToList()
            };

            return resultado;
        }
    }
}
