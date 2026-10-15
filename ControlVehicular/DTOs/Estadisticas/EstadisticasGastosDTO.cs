
namespace ControlVehicular.DTOs.Estadisticas
{
    // ==========================================
    // REQUEST
    // ==========================================

    public class EstadisticasGastosRequest
    {
        public DateOnly? FechaInicio { get; set; }

        public DateOnly? FechaFin { get; set; }

        public long? VehiculoId { get; set; }
    }

    // ==========================================
    // RESPONSE
    // ==========================================

    public class EstadisticasGastosResponse
    {
        public int CantidadGastos { get; set; }

        public DateOnly? FechaInicio { get; set; }

        public DateOnly? FechaFin { get; set; }

        public long? VehiculoId { get; set; }

        public List<TotalPorMonedaResponse> TotalesPorMoneda
        { get; set; } = new();

        public List<TotalPorVehiculoResponse> TotalesPorVehiculo
        { get; set; } = new();

        public List<TotalPorCategoriaResponse> TotalesPorCategoria
        { get; set; } = new();

        public List<TotalPorMesResponse> TotalesPorMes
        { get; set; } = new();
    }

    // ==========================================
    // RESULTADOS AGRUPADOS
    // ==========================================

    public class TotalPorMonedaResponse
    {
        public string Moneda { get; set; } = null!;

        public int Cantidad { get; set; }

        public decimal Total { get; set; }
    }

    public class TotalPorVehiculoResponse
    {
        public long VehiculoId { get; set; }

        public string Moneda { get; set; } = null!;

        public int Cantidad { get; set; }

        public decimal Total { get; set; }
    }

    public class TotalPorCategoriaResponse
    {
        public long CategoriaId { get; set; }

        public string Moneda { get; set; } = null!;

        public int Cantidad { get; set; }

        public decimal Total { get; set; }
    }

    public class TotalPorMesResponse
    {
        public string Mes { get; set; } = null!;

        public string Moneda { get; set; } = null!;

        public int Cantidad { get; set; }

        public decimal Total { get; set; }
    }
}
