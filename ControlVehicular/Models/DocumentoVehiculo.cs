
namespace ControlVehicular.Models
{
    public partial class DocumentoVehiculo
    {
        public long Id { get; set; }

        public long VehiculoId { get; set; }

        public string Tipo { get; set; } = null!;

        public string? Descripcion { get; set; }

        public string? NumeroDocumento { get; set; }

        public string? EntidadEmisora { get; set; }

        public DateOnly? FechaEmision { get; set; }

        public DateOnly? FechaVencimiento { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaRegistro { get; set; }

        public DateTime FechaActualizacion { get; set; }
    }
}
