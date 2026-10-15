
namespace ControlVehicular.DTOs.Estadisticas
{
    public class GastoJavaDTO
    {
        public long Id { get; set; }

        public long VehiculoId { get; set; }

        public long CategoriaId { get; set; }

        public decimal Monto { get; set; }

        public string Moneda { get; set; } = null!;

        public DateOnly Fecha { get; set; }

        public bool Activo { get; set; }
    }
}
