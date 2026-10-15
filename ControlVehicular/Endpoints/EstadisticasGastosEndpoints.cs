
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using ControlVehicular.DTOs.Estadisticas;
using ControlVehicular.Services.Estadisticas;

namespace ControlVehicular.Endpoints
{
    public static class EstadisticasGastosEndpoints
    {
        public static void AddEstadisticasGastos(
            this IEndpointRouteBuilder routes
        )
        {
            var group = routes
                .MapGroup("/api/estadisticas/gastos")
                .WithTags("Estadisticas de Gastos");

            // ======================================
            // OBTENER ESTADISTICAS DE GASTOS
            // ======================================

            group.MapGet("", async (
                [FromQuery] DateOnly? fechaInicio,
                [FromQuery] DateOnly? fechaFin,
                [FromQuery] long? vehiculoId,
                HttpContext context,
                IEstadisticasGastosService estadisticasService,
                CancellationToken cancellationToken
            ) =>
            {
                // Crear DTO de solicitud
                var filtros = new EstadisticasGastosRequest
                {
                    FechaInicio = fechaInicio,
                    FechaFin = fechaFin,
                    VehiculoId = vehiculoId
                };

                // Validar rango de fechas
                if (fechaInicio.HasValue &&
                    fechaFin.HasValue &&
                    fechaInicio.Value > fechaFin.Value)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "La fecha de inicio no puede ser posterior a la fecha final."
                    });
                }

                // Validar identificador del vehículo
                if (vehiculoId.HasValue && vehiculoId.Value <= 0)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "El ID del vehículo debe ser mayor que cero."
                    });
                }

                // Obtener JWT del encabezado
                var authorization = context.Request
                    .Headers.Authorization.ToString();

                if (!authorization.StartsWith(
                    "Bearer ",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Unauthorized();
                }

                var token = authorization[7..].Trim();

                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.Unauthorized();
                }

                try
                {
                    // Consultar servicio de estadísticas
                    var resultado = await estadisticasService
                        .GetEstadisticas(
                            filtros,
                            token,
                            cancellationToken
                        );

                    // 200 OK
                    return Results.Ok(resultado);
                }
                catch (ArgumentException ex)
                {
                    // 400 Bad Request
                    return Results.BadRequest(new
                    {
                        mensaje = ex.Message
                    });
                }
                catch (HttpRequestException ex)
                    when (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // 401 Unauthorized
                    return Results.Unauthorized();
                }
                catch (HttpRequestException ex)
                    when (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    // 403 Forbidden
                    return Results.Forbid();
                }
                catch (HttpRequestException)
                {
                    // 502 Bad Gateway
                    return Results.Problem(
                        "No fue posible consultar los gastos desde Java.",
                        statusCode: 502
                    );
                }
                catch (JsonException)
                {
                    // 502 Bad Gateway
                    return Results.Problem(
                        "La respuesta de gastos de Java no tiene un formato válido.",
                        statusCode: 502
                    );
                }
                catch (OperationCanceledException)
                    when (!cancellationToken.IsCancellationRequested)
                {
                    // 504 Gateway Timeout
                    return Results.Problem(
                        "La API Java excedió el tiempo de espera.",
                        statusCode: 504
                    );
                }

            })
            .WithOpenApi(o => new OpenApiOperation(o)
            {
                Summary = "Consultar estadísticas de gastos",
                Description = "Obtiene estadísticas de los gastos activos, " +
                              "agrupados por moneda, vehículo, categoría y mes. " +
                              "Permite filtrar por fechas y vehículo."
            })
            .RequireAuthorization(policy =>
                policy.RequireRole("ADMINISTRADOR")
            );
        }
    }
}
