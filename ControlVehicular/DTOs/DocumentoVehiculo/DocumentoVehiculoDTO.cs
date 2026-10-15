
namespace ControlVehicular.DTOs.DocumentoVehiculo
{
    public class DocumentoVehiculoResponse
    {
        public long Id { get; set; }

        public long VehiculoId { get; set; }

        public string Tipo { get; set; } = null!;

        public string? Descripcion { get; set; }

        public string? NumeroDocumento { get; set; }

        public string? EntidadEmisora { get; set; }

        public DateOnly? FechaEmision { get; set; }

        public DateOnly? FechaVencimiento { get; set; }

        public bool Activo { get; set; }

        public DateTime FechaRegistro { get; set; }

        public DateTime FechaActualizacion { get; set; }
    }

    public class DocumentoVehiculoRequest
    {
        public long VehiculoId { get; set; }

        public string Tipo { get; set; } = null!;

        public string? Descripcion { get; set; }

        public string? NumeroDocumento { get; set; }

        public string? EntidadEmisora { get; set; }

        public DateOnly? FechaEmision { get; set; }

        public DateOnly? FechaVencimiento { get; set; }
    }
}
