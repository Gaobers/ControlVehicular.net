
using AutoMapper;
using ControlVehicular.DTOs;
using ControlVehicular.DTOs.DocumentoVehiculo;
using ControlVehicular.Models;
using ControlVehicular.Services.Integraciones;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace ControlVehicular.Services.DocumentosVehiculo
{
    public class DocumentoVehiculoService : IDocumentoVehiculoService
    {
        private readonly ControlVehicularDbContext _db;
        private readonly IMapper _mapper;
        private readonly IJavaApiService _javaApiService;

        public DocumentoVehiculoService(
            ControlVehicularDbContext db,
            IMapper mapper,
            IJavaApiService javaApiService
        )
        {
            _db = db;
            _mapper = mapper;
            _javaApiService = javaApiService;
        }

        // ==========================================
        // OBTENER TODOS LOS DOCUMENTOS
        // ==========================================

        public async Task<List<DocumentoVehiculoResponse>> GetDocumentos(
            long? vehiculoId,
            bool incluirInactivos
        )
        {
            var consulta = _db.DocumentosVehiculo
                .AsNoTracking()
                .AsQueryable();

            if (vehiculoId.HasValue)
            {
                consulta = consulta.Where(d =>
                    d.VehiculoId == vehiculoId.Value
                );
            }

            if (!incluirInactivos)
            {
                consulta = consulta.Where(d => d.Activo);
            }

            var documentos = await consulta
                .OrderByDescending(d => d.FechaRegistro)
                .ToListAsync();

            return _mapper.Map<
                List<DocumentoVehiculoResponse>
            >(documentos);
        }

        // ==========================================
        // OBTENER DOCUMENTO POR ID
        // ==========================================

        public async Task<DocumentoVehiculoResponse?> GetDocumento(
            long documentoId
        )
        {
            var documento = await _db.DocumentosVehiculo
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentoId);

            if (documento == null)
                return null;

            return _mapper.Map<DocumentoVehiculoResponse>(documento);
        }

        // ==========================================
        // CREAR DOCUMENTO
        // ==========================================

        public async Task<long> PostDocumento(
            DocumentoVehiculoRequest documento,
            string token,
            CancellationToken cancellationToken = default
        )
        {
            ValidarDocumento(documento);

            await VerificarVehiculo(
                documento.VehiculoId,
                token,
                cancellationToken
            );

            var entidad = _mapper.Map<DocumentoVehiculo>(documento);

            entidad.Tipo = documento.Tipo.Trim().ToUpperInvariant();
            entidad.Activo = true;

            await _db.DocumentosVehiculo.AddAsync(
                entidad,
                cancellationToken
            );

            await _db.SaveChangesAsync(cancellationToken);

            return entidad.Id;
        }

        // ==========================================
        // MODIFICAR DOCUMENTO
        // ==========================================

        public async Task<int> PutDocumento(
            long documentoId,
            DocumentoVehiculoRequest documento,
            string token,
            CancellationToken cancellationToken = default
        )
        {
            var entidad = await _db.DocumentosVehiculo
                .FindAsync(new object[] { documentoId }, cancellationToken);

            if (entidad == null)
                return -1;

            ValidarDocumento(documento);

            // Verificar el vehículo si cambia su asociación.
            if (entidad.VehiculoId != documento.VehiculoId)
            {
                await VerificarVehiculo(
                    documento.VehiculoId,
                    token,
                    cancellationToken
                );
            }

            _mapper.Map(documento, entidad);

            entidad.Tipo = documento.Tipo.Trim().ToUpperInvariant();

            return await _db.SaveChangesAsync(cancellationToken);
        }

        // ==========================================
        // DESACTIVAR DOCUMENTO
        // ==========================================

        public async Task<int> DeleteDocumento(long documentoId)
        {
            var entidad = await _db.DocumentosVehiculo
                .FindAsync(documentoId);

            if (entidad == null)
                return -1;

            if (!entidad.Activo)
                return 0;

            entidad.Activo = false;

            return await _db.SaveChangesAsync();
        }

        // ==========================================
        // REACTIVAR DOCUMENTO
        // ==========================================

        public async Task<int> ActivarDocumento(long documentoId)
        {
            var entidad = await _db.DocumentosVehiculo
                .FindAsync(documentoId);

            if (entidad == null)
                return -1;

            if (entidad.Activo)
                return 0;

            entidad.Activo = true;

            return await _db.SaveChangesAsync();
        }

        // ==========================================
        // VALIDAR VEHICULO EN JAVA
        // ==========================================

        private async Task VerificarVehiculo(
            long vehiculoId,
            string token,
            CancellationToken cancellationToken
        )
        {
            var respuesta = await _javaApiService.ObtenerVehiculoAsync(
                vehiculoId,
                token,
                cancellationToken
            );

            if (respuesta.EstadoHttp == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException(
                    "El vehículo no existe en la API Java."
                );
            }

            if (respuesta.EstadoHttp != HttpStatusCode.OK)
            {
                throw new HttpRequestException(
                    "No fue posible validar el vehículo en Java.",
                    null,
                    respuesta.EstadoHttp
                );
            }
        }

        // ==========================================
        // VALIDACIONES
        // ==========================================

        private static void ValidarDocumento(
            DocumentoVehiculoRequest documento
        )
        {
            if (documento.VehiculoId <= 0)
            {
                throw new ArgumentException(
                    "Debe seleccionar un vehículo válido."
                );
            }

            if (string.IsNullOrWhiteSpace(documento.Tipo))
            {
                throw new ArgumentException(
                    "El tipo de documento es obligatorio."
                );
            }

            var tipo = documento.Tipo.Trim().ToUpperInvariant();

            if (tipo != "SEGURO" &&
                tipo != "REVISION_TECNICA" &&
                tipo != "OTRO")
            {
                throw new ArgumentException(
                    "El tipo debe ser SEGURO, REVISION_TECNICA u OTRO."
                );
            }

            if (documento.Descripcion?.Length > 150)
            {
                throw new ArgumentException(
                    "La descripción no puede superar 150 caracteres."
                );
            }

            if (documento.NumeroDocumento?.Length > 100)
            {
                throw new ArgumentException(
                    "El número de documento no puede superar 100 caracteres."
                );
            }

            if (documento.EntidadEmisora?.Length > 150)
            {
                throw new ArgumentException(
                    "La entidad emisora no puede superar 150 caracteres."
                );
            }

            if (documento.FechaEmision.HasValue &&
                documento.FechaVencimiento.HasValue &&
                documento.FechaVencimiento.Value <
                documento.FechaEmision.Value)
            {
                throw new ArgumentException(
                    "La fecha de vencimiento no puede ser anterior a la emisión."
                );
            }
        }
    }
}
