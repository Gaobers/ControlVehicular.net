
using Microsoft.EntityFrameworkCore;

namespace ControlVehicular.Models
{
    public partial class ControlVehicularDbContext : DbContext
    {
        public ControlVehicularDbContext(
            DbContextOptions<ControlVehicularDbContext> options
        ) : base(options)
        {
        }

        // ==========================================
        // TABLAS DE SQL SERVER
        // ==========================================

        // Aquí agregaremos las entidades
        // correspondientes a los módulos de .NET.

        // ==========================================
        // CONFIGURACIÓN DE MODELOS
        // ==========================================

        protected override void OnModelCreating(
            ModelBuilder modelBuilder
        )
        {
            base.OnModelCreating(modelBuilder);

            // Configuraciones de entidades futuras.
        }
    }
}
