using ETL.Core.Models;
using ETL.Data.Helpers;
using ETL.Data.Loaders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ETL.Console;

public class EtlOrchestrator
{
    private readonly CategoriasLoader _categorias;
    private readonly PaisesLoader _paises;
    private readonly ProductosLoader _productos;
    private readonly ClientesLoader _clientes;
    private readonly OrdenesLoader _ordenes;
    private readonly DetalleOrdenesLoader _detalle;
    private readonly AuditoriaService _auditoria;
    private readonly IConfiguration _config;
    private readonly ILogger<EtlOrchestrator> _log;

    private readonly List<(string Archivo, ETL.Core.Interfaces.EtlResult Result)> _resultados = new();

    public EtlOrchestrator(
        CategoriasLoader categorias,
        PaisesLoader paises,
        ProductosLoader productos,
        ClientesLoader clientes,
        OrdenesLoader ordenes,
        DetalleOrdenesLoader detalle,
        AuditoriaService auditoria,
        IConfiguration config,
        ILogger<EtlOrchestrator> log)
    {
        _categorias = categorias;
        _paises = paises;
        _productos = productos;
        _clientes = clientes;
        _ordenes = ordenes;
        _detalle = detalle;
        _auditoria = auditoria;
        _config = config;
        _log = log;
    }

    public async Task EjecutarAsync()
    {
        var paths = _config.GetSection("CsvPaths");
        string csvProductos = paths["Productos"]!;
        string csvClientes = paths["Clientes"]!;
        string csvOrdenes = paths["Ordenes"]!;
        string csvDetalleOrdenes = paths["DetalleOrdenes"]!;

        var inicio = DateTime.Now;

        System.Console.WriteLine($"Inicio del proceso ETL: {inicio:dd/MM/yyyy HH:mm:ss}");
        System.Console.WriteLine($"Base de datos: SistemaVentasDB @ localhost\\SQLEXPRESS");
        System.Console.WriteLine(new string('-', 60));

        await EjecutarPasoAsync("Categorias", csvProductos, _categorias.CargarAsync);
        await EjecutarPasoAsync("Paises", csvClientes, _paises.CargarAsync);
        await EjecutarPasoAsync("Productos", csvProductos, _productos.CargarAsync);
        await EjecutarPasoAsync("Clientes", csvClientes, _clientes.CargarAsync);
        await EjecutarPasoAsync("Ordenes", csvOrdenes, _ordenes.CargarAsync);
        await EjecutarPasoAsync("DetalleOrdenes", csvDetalleOrdenes, _detalle.CargarAsync);

        MostrarResumen(inicio);
    }

    private async Task EjecutarPasoAsync(
        string nombreEntidad,
        string rutaArchivo,
        Func<string, Task<ETL.Core.Interfaces.EtlResult>> loader)
    {
        System.Console.WriteLine();
        System.Console.WriteLine($"  Cargando {nombreEntidad}...");

        var ini = DateTime.Now;
        ETL.Core.Interfaces.EtlResult result;

        try
        {
            result = await loader(rutaArchivo);
        }
        catch (Exception ex)
        {
            result = new ETL.Core.Interfaces.EtlResult(
                nombreEntidad, 0, 0, 0, [$"Error inesperado: {ex.Message}"]);
        }

        var duracion = DateTime.Now - ini;

        System.Console.ForegroundColor = result.Rechazados == 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
        System.Console.WriteLine($"  {result.Entidad,-18} | Total: {result.Totales,6} | Insertados: {result.Insertados,6} | Rechazados: {result.Rechazados,5} | {duracion.TotalSeconds:F1}s");
        System.Console.ResetColor();

        if (result.Errores.Count > 0)
        {
            System.Console.ForegroundColor = ConsoleColor.DarkYellow;
            foreach (var e in result.Errores.Take(3))
                System.Console.WriteLine($"    - {e}");
            if (result.Errores.Count > 3)
                System.Console.WriteLine($"    - ... y {result.Errores.Count - 3} error(es) mas.");
            System.Console.ResetColor();
        }

        _resultados.Add((rutaArchivo, result));

        await _auditoria.RegistrarAsync(new AuditoriaETL
        {
            TipoFuente = "CSV",
            NombreArchivo = Path.GetFileName(rutaArchivo),
            RutaArchivo = rutaArchivo,
            Extension = Path.GetExtension(rutaArchivo),
            RegistrosTotales = result.Totales,
            RegistrosInsertados = result.Insertados,
            RegistrosRechazados = result.Rechazados,
            Estado = result.Errores.Count == 0 ? "EXITOSO" : "PARCIAL",
            MensajeError = result.Errores.Count > 0
                ? string.Join(" | ", result.Errores.Take(5))
                : null
        });
    }

    private void MostrarResumen(DateTime inicio)
    {
        var durTotal = DateTime.Now - inicio;
        int totTotal = _resultados.Sum(x => x.Result.Totales);
        int insTotal = _resultados.Sum(x => x.Result.Insertados);
        int rejTotal = _resultados.Sum(x => x.Result.Rechazados);

        System.Console.WriteLine();
        System.Console.WriteLine(new string('-', 60));
        System.Console.WriteLine("RESUMEN DEL PROCESO ETL");
        System.Console.WriteLine(new string('-', 60));
        System.Console.WriteLine($"{"Entidad",-20} {"Total",8} {"Insertados",12} {"Rechazados",12}");
        System.Console.WriteLine(new string('-', 60));

        foreach (var (_, r) in _resultados)
        {
            var pct = r.Totales > 0 ? (r.Insertados * 100.0 / r.Totales) : 0;
            System.Console.WriteLine($"{r.Entidad,-20} {r.Totales,8} {r.Insertados,12} {r.Rechazados,10} ({pct:F1}%)");
        }

        System.Console.WriteLine(new string('-', 60));
        System.Console.WriteLine($"{"TOTAL",-20} {totTotal,8} {insTotal,12} {rejTotal,12}");
        System.Console.WriteLine($"Duracion total: {durTotal.TotalSeconds:F1}s");
        System.Console.WriteLine(new string('-', 60));
    }
}