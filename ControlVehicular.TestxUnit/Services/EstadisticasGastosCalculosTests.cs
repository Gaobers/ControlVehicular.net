
using ControlVehicular.DTOs.Estadisticas;
using ControlVehicular.Services.Estadisticas;
using ControlVehicular.Services.Integraciones;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace ControlVehicular.TestxUnit.Services;

public class EstadisticasGastosCalculosTests
{
    // ==========================================
    // PREPARAR DATOS DE PRUEBA
    // ==========================================

    private static GastoJavaDTO Gasto(
        long id,
        long vehiculoId,
        long categoriaId,
        decimal monto,
        string moneda,
        int mes,
        int dia,
        bool activo = true)
    {
        return new GastoJavaDTO
        {
            Id = id,
            VehiculoId = vehiculoId,
            CategoriaId = categoriaId,
            Monto = monto,
            Moneda = moneda,
            Fecha = new DateOnly(2026, mes, dia),
            Activo = activo
        };
    }

    private static List<GastoJavaDTO> GastosEjemplo()
    {
        return new List<GastoJavaDTO>
        {
            Gasto(1, 100, 10, 50m, " usd ", 1, 5),
            Gasto(2, 100, 20, 100m, "USD", 1, 20),
            Gasto(3, 200, 10, 30m, "usd", 1, 25),
            Gasto(4, 200, 20, 20m, "USD", 2, 10),
            Gasto(5, 100, 10, 80m, " svc ", 2, 12),
            Gasto(6, 200, 20, 70m, "SVC", 2, 15),
            Gasto(7, 100, 20, 900m, "USD", 3, 1, false),
            Gasto(8, 300, 10, 10m, "USD", 3, 5)
        };
    }

    // ==========================================
    // SIMULAR RESPUESTA DE JAVA
    // ==========================================

    private static EstadisticasGastosService CrearServicio(
        IEnumerable<GastoJavaDTO> gastos)
    {
        var opciones = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(gastos, opciones);

        var java = new Mock<IJavaApiService>();

        java.Setup(x => x.ObtenerGastosAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JavaApiResponse(
                HttpStatusCode.OK,
                json,
                "application/json"));

        return new EstadisticasGastosService(java.Object);
    }

    private static Task<EstadisticasGastosResponse> Ejecutar(
        IEnumerable<GastoJavaDTO> gastos,
        EstadisticasGastosRequest? filtros = null)
    {
        var servicio = CrearServicio(gastos);

        return servicio.GetEstadisticas(
            filtros ?? new EstadisticasGastosRequest(),
            "token-prueba");
    }

    // ==========================================
    // 1. CONTABILIZAR SOLO GASTOS ACTIVOS
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_ExcluyeGastosInactivos()
    {
        var resultado = await Ejecutar(GastosEjemplo());

        Assert.Equal(7, resultado.CantidadGastos);
    }

    // ==========================================
    // 2. AGRUPAR POR MONEDA
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_AgrupaPorMoneda()
    {
        var resultado = await Ejecutar(GastosEjemplo());

        Assert.Equal(2, resultado.TotalesPorMoneda.Count);

        var usd = Assert.Single(
            resultado.TotalesPorMoneda
                .Where(x => x.Moneda == "USD"));

        Assert.Equal(5, usd.Cantidad);
        Assert.Equal(210m, usd.Total);

        var svc = Assert.Single(
            resultado.TotalesPorMoneda
                .Where(x => x.Moneda == "SVC"));

        Assert.Equal(2, svc.Cantidad);
        Assert.Equal(150m, svc.Total);

        // Verificar orden alfabético.
        Assert.Equal("SVC", resultado.TotalesPorMoneda[0].Moneda);
        Assert.Equal("USD", resultado.TotalesPorMoneda[1].Moneda);
    }

    // ==========================================
    // 3. AGRUPAR POR VEHICULO Y MONEDA
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_AgrupaPorVehiculoYMoneda()
    {
        var resultado = await Ejecutar(GastosEjemplo());

        Assert.Equal(5, resultado.TotalesPorVehiculo.Count);

        var vehiculo100Usd = Assert.Single(
            resultado.TotalesPorVehiculo.Where(x =>
                x.VehiculoId == 100 && x.Moneda == "USD"));

        Assert.Equal(2, vehiculo100Usd.Cantidad);
        Assert.Equal(150m, vehiculo100Usd.Total);

        var vehiculo100Svc = Assert.Single(
            resultado.TotalesPorVehiculo.Where(x =>
                x.VehiculoId == 100 && x.Moneda == "SVC"));

        Assert.Equal(1, vehiculo100Svc.Cantidad);
        Assert.Equal(80m, vehiculo100Svc.Total);

        var vehiculo200Usd = Assert.Single(
            resultado.TotalesPorVehiculo.Where(x =>
                x.VehiculoId == 200 && x.Moneda == "USD"));

        Assert.Equal(2, vehiculo200Usd.Cantidad);
        Assert.Equal(50m, vehiculo200Usd.Total);

        var vehiculo200Svc = Assert.Single(
            resultado.TotalesPorVehiculo.Where(x =>
                x.VehiculoId == 200 && x.Moneda == "SVC"));

        Assert.Equal(1, vehiculo200Svc.Cantidad);
        Assert.Equal(70m, vehiculo200Svc.Total);

        var vehiculo300Usd = Assert.Single(
            resultado.TotalesPorVehiculo.Where(x =>
                x.VehiculoId == 300 && x.Moneda == "USD"));

        Assert.Equal(1, vehiculo300Usd.Cantidad);
        Assert.Equal(10m, vehiculo300Usd.Total);
    }

    // ==========================================
    // 4. AGRUPAR POR CATEGORIA Y MONEDA
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_AgrupaPorCategoria()
    {
        var resultado = await Ejecutar(GastosEjemplo());

        Assert.Equal(4, resultado.TotalesPorCategoria.Count);

        var categoria10Usd = Assert.Single(
            resultado.TotalesPorCategoria.Where(x =>
                x.CategoriaId == 10 && x.Moneda == "USD"));

        Assert.Equal(3, categoria10Usd.Cantidad);
        Assert.Equal(90m, categoria10Usd.Total);

        var categoria20Usd = Assert.Single(
            resultado.TotalesPorCategoria.Where(x =>
                x.CategoriaId == 20 && x.Moneda == "USD"));

        Assert.Equal(2, categoria20Usd.Cantidad);
        Assert.Equal(120m, categoria20Usd.Total);

        var categoria10Svc = Assert.Single(
            resultado.TotalesPorCategoria.Where(x =>
                x.CategoriaId == 10 && x.Moneda == "SVC"));

        Assert.Equal(80m, categoria10Svc.Total);

        var categoria20Svc = Assert.Single(
            resultado.TotalesPorCategoria.Where(x =>
                x.CategoriaId == 20 && x.Moneda == "SVC"));

        Assert.Equal(70m, categoria20Svc.Total);
    }

    // ==========================================
    // 5. AGRUPAR POR MES Y MONEDA
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_AgrupaPorMes()
    {
        var resultado = await Ejecutar(GastosEjemplo());

        Assert.Equal(4, resultado.TotalesPorMes.Count);

        var enero = Assert.Single(
            resultado.TotalesPorMes.Where(x =>
                x.Mes == "2026-01" && x.Moneda == "USD"));

        Assert.Equal(3, enero.Cantidad);
        Assert.Equal(180m, enero.Total);

        var febreroUsd = Assert.Single(
            resultado.TotalesPorMes.Where(x =>
                x.Mes == "2026-02" && x.Moneda == "USD"));

        Assert.Equal(1, febreroUsd.Cantidad);
        Assert.Equal(20m, febreroUsd.Total);

        var febreroSvc = Assert.Single(
            resultado.TotalesPorMes.Where(x =>
                x.Mes == "2026-02" && x.Moneda == "SVC"));

        Assert.Equal(2, febreroSvc.Cantidad);
        Assert.Equal(150m, febreroSvc.Total);

        var marzo = Assert.Single(
            resultado.TotalesPorMes.Where(x =>
                x.Mes == "2026-03" && x.Moneda == "USD"));

        Assert.Equal(1, marzo.Cantidad);
        Assert.Equal(10m, marzo.Total);
    }

    // ==========================================
    // 6. FILTRAR POR VEHICULO
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_FiltraPorVehiculo()
    {
        var filtros = new EstadisticasGastosRequest
        {
            VehiculoId = 100
        };

        var resultado = await Ejecutar(
            GastosEjemplo(), filtros);

        Assert.Equal(3, resultado.CantidadGastos);
        Assert.Equal(100L, resultado.VehiculoId);

        Assert.All(resultado.TotalesPorVehiculo,
            x => Assert.Equal(100L, x.VehiculoId));

        var usd = Assert.Single(
            resultado.TotalesPorMoneda.Where(x =>
                x.Moneda == "USD"));

        Assert.Equal(150m, usd.Total);

        var svc = Assert.Single(
            resultado.TotalesPorMoneda.Where(x =>
                x.Moneda == "SVC"));

        Assert.Equal(80m, svc.Total);
    }

    // ==========================================
    // 7. FILTRAR POR FECHA INICIAL
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_FiltraPorFechaInicio()
    {
        var filtros = new EstadisticasGastosRequest
        {
            FechaInicio = new DateOnly(2026, 2, 12)
        };

        var resultado = await Ejecutar(
            GastosEjemplo(), filtros);

        Assert.Equal(3, resultado.CantidadGastos);
        Assert.Equal(filtros.FechaInicio, resultado.FechaInicio);

        var svc = Assert.Single(
            resultado.TotalesPorMoneda.Where(x =>
                x.Moneda == "SVC"));

        Assert.Equal(150m, svc.Total);
        Assert.Equal(2, svc.Cantidad);

        var usd = Assert.Single(
            resultado.TotalesPorMoneda.Where(x =>
                x.Moneda == "USD"));

        Assert.Equal(10m, usd.Total);
    }

    // ==========================================
    // 8. FILTRAR POR FECHA FINAL
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_FiltraPorFechaFin()
    {
        var filtros = new EstadisticasGastosRequest
        {
            FechaFin = new DateOnly(2026, 1, 20)
        };

        var resultado = await Ejecutar(
            GastosEjemplo(), filtros);

        Assert.Equal(2, resultado.CantidadGastos);
        Assert.Equal(filtros.FechaFin, resultado.FechaFin);

        var usd = Assert.Single(resultado.TotalesPorMoneda);

        Assert.Equal("USD", usd.Moneda);
        Assert.Equal(150m, usd.Total);
    }

    // ==========================================
    // 9. COMBINAR FILTROS Y COMPROBAR LIMITES
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_CombinaFiltros()
    {
        var filtros = new EstadisticasGastosRequest
        {
            VehiculoId = 200,
            FechaInicio = new DateOnly(2026, 1, 25),
            FechaFin = new DateOnly(2026, 2, 10)
        };

        var resultado = await Ejecutar(
            GastosEjemplo(), filtros);

        Assert.Equal(2, resultado.CantidadGastos);
        Assert.Equal(200L, resultado.VehiculoId);
        Assert.Equal(filtros.FechaInicio, resultado.FechaInicio);
        Assert.Equal(filtros.FechaFin, resultado.FechaFin);

        var total = Assert.Single(resultado.TotalesPorMoneda);

        Assert.Equal("USD", total.Moneda);
        Assert.Equal(2, total.Cantidad);
        Assert.Equal(50m, total.Total);
    }

    // ==========================================
    // 10. FILTRO SIN COINCIDENCIAS
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_SinCoincidencias_RetornaVacio()
    {
        var filtros = new EstadisticasGastosRequest
        {
            VehiculoId = 999
        };

        var resultado = await Ejecutar(
            GastosEjemplo(), filtros);

        Assert.Equal(0, resultado.CantidadGastos);
        Assert.Empty(resultado.TotalesPorMoneda);
        Assert.Empty(resultado.TotalesPorVehiculo);
        Assert.Empty(resultado.TotalesPorCategoria);
        Assert.Empty(resultado.TotalesPorMes);
    }

    // ==========================================
    // 11. VALIDAR RANGO DE FECHAS
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_FechasInvalidas_LanzaError()
    {
        var filtros = new EstadisticasGastosRequest
        {
            FechaInicio = new DateOnly(2026, 6, 20),
            FechaFin = new DateOnly(2026, 6, 10)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => Ejecutar(GastosEjemplo(), filtros));
    }

    // ==========================================
    // 12. COMPROBAR PRECISION DECIMAL
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_SumaDecimalesCorrectamente()
    {
        var gastos = new List<GastoJavaDTO>
        {
            Gasto(1, 100, 10, 0.10m, "USD", 1, 1),
            Gasto(2, 100, 10, 0.20m, "USD", 1, 2)
        };

        var resultado = await Ejecutar(gastos);

        var total = Assert.Single(resultado.TotalesPorMoneda);

        Assert.Equal(2, total.Cantidad);
        Assert.Equal(0.30m, total.Total);
    }

    // ==========================================
    // 13. TODOS LOS GASTOS INACTIVOS
    // ==========================================

    [Fact]
    public async Task GetEstadisticas_TodosInactivos_RetornaCero()
    {
        var gastos = new List<GastoJavaDTO>
        {
            Gasto(1, 100, 10, 50m, "USD", 1, 1, false),
            Gasto(2, 100, 10, 80m, "USD", 1, 2, false)
        };

        var resultado = await Ejecutar(gastos);

        Assert.Equal(0, resultado.CantidadGastos);
        Assert.Empty(resultado.TotalesPorMoneda);
        Assert.Empty(resultado.TotalesPorVehiculo);
        Assert.Empty(resultado.TotalesPorCategoria);
        Assert.Empty(resultado.TotalesPorMes);
    }

    // ==========================================
    // 14-18. VALIDACIONES DE DATOS DESDE JAVA
    // ==========================================

    [Theory]
    [InlineData("Id")]
    [InlineData("VehiculoId")]
    [InlineData("CategoriaId")]
    [InlineData("Moneda")]
    [InlineData("Fecha")]
    public async Task GetEstadisticas_DatoIncompleto_LanzaError(
        string campo)
    {
        var gasto = Gasto(
            1, 100, 10, 50m, "USD", 1, 1);

        switch (campo)
        {
            case "Id":
                gasto.Id = 0;
                break;

            case "VehiculoId":
                gasto.VehiculoId = 0;
                break;

            case "CategoriaId":
                gasto.CategoriaId = 0;
                break;

            case "Moneda":
                gasto.Moneda = "  ";
                break;

            case "Fecha":
                gasto.Fecha = default;
                break;
        }

        await Assert.ThrowsAsync<JsonException>(
            () => Ejecutar(new[] { gasto }));
    }
}
