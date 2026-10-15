
using ControlVehicular.DTOs;
using ControlVehicular.DTOs.DocumentoVehiculo;

namespace ControlVehicular.Services.DocumentosVehiculo
{
    public interface IDocumentoVehiculoService
    {
        Task<long> PostDocumento(
            DocumentoVehiculoRequest documento,
            string token,
            CancellationToken cancellationToken = default
        );

        Task<List<DocumentoVehiculoResponse>> GetDocumentos(
            long? vehiculoId,
            bool incluirInactivos
        );

        Task<DocumentoVehiculoResponse?> GetDocumento(
            long documentoId
        );

        Task<int> PutDocumento(
            long documentoId,
            DocumentoVehiculoRequest documento,
            string token,
            CancellationToken cancellationToken = default
        );

        Task<int> DeleteDocumento(long documentoId);

        Task<int> ActivarDocumento(long documentoId);
    }
}
