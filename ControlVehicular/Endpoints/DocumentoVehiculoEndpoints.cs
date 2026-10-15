
using ControlVehicular.DTOs;
using ControlVehicular.DTOs.DocumentoVehiculo;
using ControlVehicular.Services.DocumentosVehiculo;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Net;

namespace ControlVehicular.Endpoints
{
    public static class DocumentoVehiculoEndpoints
    {
        public static void AddDocumentoVehiculoEndpoints(
            this IEndpointRouteBuilder routes
        )
        {
            var group = routes
                .MapGroup("/api/documentos-vehiculos")
                .WithTags("Documentos de Vehiculos")
                .RequireAuthorization(policy =>
                    policy.RequireRole("ADMINISTRADOR")
                );

            // ======================================
            // OBTENER TODOS
            // ======================================

            group.MapGet("/", async (
                long? vehiculoId,
                bool? incluirInactivos,
                IDocumentoVehiculoService service
            ) =>
            {
                if (vehiculoId.HasValue && vehiculoId <= 0)
                    return Results.BadRequest();

                var documentos = await service.GetDocumentos(
                    vehiculoId,
                    incluirInactivos ?? false
                );

                return Results.Ok(documentos);

            }).WithOpenApi(o => new OpenApiOperation(o)
            {
                Summary = "Obtener documentos",
                Description = "Lista documentos y permite filtrar por vehículo."
            });

            // ======================================
            // OBTENER POR ID
            // ======================================

            group.MapGet("/{id:long}", async (
                long id,
                IDocumentoVehiculoService service
            ) =>
            {
                var documento = await service.GetDocumento(id);

                if (documento == null)
                    return Results.NotFound();

                return Results.Ok(documento);

            }).WithOpenApi(o => new OpenApiOperation(o)
            {
                Summary = "Obtener documento",
                Description = "Consulta un documento por su identificador."
            });

            // ======================================
            // CREAR DOCUMENTO
            // ======================================

            group.MapPost("/", async (
                DocumentoVehiculoRequest documento,
                HttpContext context,
                IDocumentoVehiculoService service,
                CancellationToken cancellationToken
            ) =>
            {
                return await Ejecutar(async () =>
                {
                    var token = ObtenerToken(context);

                    if (token == null)
                        return Results.Unauthorized();

                    var id = await service.PostDocumento(
                        documento,
                        token,
                        cancellationToken
                    );

                    return Results.Created(
                        $"/api/documentos-vehiculos/{id}",
                        new { id }
                    );
                });

            }).WithOpenApi(o => new OpenApiOperation(o)
            {
                Summary = "Registrar documento",
                Description = "Registra un documento asociado a un vehículo de Java."
            });

            // ======================================
            // MODIFICAR DOCUMENTO
            // ======================================

            group.MapPut("/{id:long}", async (
                long id,
                DocumentoVehiculoRequest documento,
                HttpContext context,
                IDocumentoVehiculoService service,
                CancellationToken cancellationToken
            ) =>
            {
                return await Ejecutar(async () =>
                {
                    var token = ObtenerToken(context);

                    if (token == null)
                        return Results.Unauthorized();

                    var resultado = await service.PutDocumento(
                        id,
                        documento,
                        token,
                        cancellationToken
                    );

                    if (resultado == -1)
                        return Results.NotFound();

                    return Results.Ok(new
                    {
                        mensaje = "Documento actualizado correctamente."
                    });
                });

            }).WithOpenApi(o => new OpenApiOperation(o)
            {
                Summary = "Modificar documento",
                Description = "Actualiza la información de un documento."
            });

            // ======================================
            // DESACTIVAR DOCUMENTO
            // ======================================

            group.MapDelete("/{id:long}", async (
                long id,
                IDocumentoVehiculoService service
            ) =>
            {
                var resultado = await service.DeleteDocumento(id);

                if (resultado == -1)
                    return Results.NotFound();

                return Results.Ok(new
                {
                    mensaje = "Documento desactivado correctamente."
                });

            }).WithOpenApi(o => new OpenApiOperation(o)
            {
                Summary = "Desactivar documento",
                Description = "Desactiva un documento sin eliminarlo físicamente."
            });

            // ======================================
            // REACTIVAR DOCUMENTO
            // ======================================

            group.MapPut("/{id:long}/activar", async (
                long id,
                IDocumentoVehiculoService service
            ) =>
            {
                var resultado = await service.ActivarDocumento(id);

                if (resultado == -1)
                    return Results.NotFound();

                return Results.Ok(new
                {
                    mensaje = "Documento activado correctamente."
                });

            }).WithOpenApi(o => new OpenApiOperation(o)
            {
                Summary = "Activar documento",
                Description = "Reactiva un documento desactivado."
            });
        }

        // ==========================================
        // OBTENER JWT
        // ==========================================

        private static string? ObtenerToken(HttpContext context)
        {
            var authorization = context.Request.Headers
                .Authorization.ToString();

            if (!authorization.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = authorization[7..].Trim();

            return string.IsNullOrWhiteSpace(token)
                ? null
                : token;
        }

        // ==========================================
        // MANEJO DE ERRORES
        // ==========================================

        private static async Task<IResult> Ejecutar(
            Func<Task<IResult>> accion
        )
        {
            try
            {
                return await accion();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new
                {
                    mensaje = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new
                {
                    mensaje = ex.Message
                });
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Results.Unauthorized();
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                return Results.Forbid();
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    "No fue posible verificar el vehículo en Java.",
                    statusCode: 502
                );
            }
            catch (OperationCanceledException)
            {
                return Results.Problem(
                    "La solicitud excedió el tiempo de espera.",
                    statusCode: 504
                );
            }
            catch (DbUpdateException)
            {
                return Results.Problem(
                    "La base de datos rechazó la operación.",
                    statusCode: 409
                );
            }
        }
    }
}
