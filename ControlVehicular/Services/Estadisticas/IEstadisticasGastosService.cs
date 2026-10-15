
using ControlVehicular.DTOs.Estadisticas;

namespace ControlVehicular.Services.Estadisticas
{
    public interface IEstadisticasGastosService
    {
        Task<EstadisticasGastosResponse> GetEstadisticas(
            EstadisticasGastosRequest filtros,
            string token,
            CancellationToken cancellationToken = default
        );
    }
}
