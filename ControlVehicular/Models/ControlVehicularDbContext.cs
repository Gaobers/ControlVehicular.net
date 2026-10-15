
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


            // ==========================================
            // TABLA: DOCUMENTO VEHICULO
            // ==========================================

            modelBuilder.Entity<DocumentoVehiculo>(entity =>
            {
                entity.ToTable("documento_vehiculo", "dbo", table =>
                {
                    // Indica a EF que la tabla tiene un trigger.
                    // No crea ni modifica el trigger en SQL Server.
                    table.HasTrigger("trg_documento_fecha_actualizacion");
                });

                entity.HasKey(e => e.Id)
                    .HasName("pk_documento_vehiculo");

                entity.HasIndex(e => e.VehiculoId)
                    .HasDatabaseName("idx_documento_vehiculo");

                entity.HasIndex(e => e.Tipo)
                    .HasDatabaseName("idx_documento_tipo");

                entity.HasIndex(e => e.FechaVencimiento)
                    .HasDatabaseName("idx_documento_vencimiento");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .UseIdentityColumn(1, 1);

                entity.Property(e => e.VehiculoId)
                    .HasColumnName("vehiculo_id")
                    .IsRequired();

                entity.Property(e => e.Tipo)
                    .HasColumnName("tipo")
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(e => e.Descripcion)
                    .HasColumnName("descripcion")
                    .HasMaxLength(150);

                entity.Property(e => e.NumeroDocumento)
                    .HasColumnName("numero_documento")
                    .HasMaxLength(100);

                entity.Property(e => e.EntidadEmisora)
                    .HasColumnName("entidad_emisora")
                    .HasMaxLength(150);

                entity.Property(e => e.FechaEmision)
                    .HasColumnName("fecha_emision")
                    .HasColumnType("date");

                entity.Property(e => e.FechaVencimiento)
                    .HasColumnName("fecha_vencimiento")
                    .HasColumnType("date");

                entity.Property(e => e.Activo)
                    .HasColumnName("activo")
                    .HasDefaultValue(true)
                    .IsRequired();

                entity.Property(e => e.FechaRegistro)
                    .HasColumnName("fecha_registro")
                    .HasColumnType("datetime2(0)")
                    .HasDefaultValueSql("SYSDATETIME()");

                entity.Property(e => e.FechaActualizacion)
                    .HasColumnName("fecha_actualizacion")
                    .HasColumnType("datetime2(0)")
                    .HasDefaultValueSql("SYSDATETIME()")
                    .ValueGeneratedOnAddOrUpdate();
            });

        }

        public virtual DbSet<DocumentoVehiculo> DocumentosVehiculo
        { get; set; } = null!;
    }
}
