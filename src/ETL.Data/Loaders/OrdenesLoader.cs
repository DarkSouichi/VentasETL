using ETL.Core.DTOs;
using ETL.Core.Interfaces;
using ETL.Data.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ETL.Data.Loaders;

public class OrdenesLoader : IEtlLoader
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<OrdenesLoader> _log;

    private static readonly HashSet<string> EstadosValidos =
        new(StringComparer.OrdinalIgnoreCase)
        { "Pending", "Shipped", "Delivered", "Cancelled" };

    public OrdenesLoader(DatabaseHelper db, ILogger<OrdenesLoader> log)
        => (_db, _log) = (db, log);

    public async Task<EtlResult> CargarAsync(string rutaArchivo)
    {
        var errores    = new List<string>();
        int insertados = 0, rechazados = 0;

        List<OrdenCsv> filas;
        try { filas = CsvReaderHelper.Leer<OrdenCsv>(rutaArchivo); }
        catch (Exception ex)
        {
            return new EtlResult("Ordenes", 0, 0, 0, [$"Error leyendo CSV: {ex.Message}"]);
        }

        var vistos  = new HashSet<int>();
        int totales = filas.Count;

        foreach (var fila in filas)
        {
            var errFila = new List<string>();

            if (string.IsNullOrWhiteSpace(fila.OrderID))    errFila.Add("OrderID vacío");
            if (string.IsNullOrWhiteSpace(fila.CustomerID)) errFila.Add("CustomerID vacío");
            if (string.IsNullOrWhiteSpace(fila.OrderDate))  errFila.Add("OrderDate vacío");
            if (string.IsNullOrWhiteSpace(fila.Status))     errFila.Add("Status vacío");

            if (!int.TryParse(fila.OrderID, out int orderId))
                errFila.Add($"OrderID no es entero: '{fila.OrderID}'");

            if (!int.TryParse(fila.CustomerID, out int clienteId))
                errFila.Add($"CustomerID no es entero: '{fila.CustomerID}'");

            if (!DateTime.TryParse(fila.OrderDate, out DateTime fecha))
                errFila.Add($"OrderDate formato inválido: '{fila.OrderDate}'");

            if (!string.IsNullOrWhiteSpace(fila.Status) && !EstadosValidos.Contains(fila.Status.Trim()))
                errFila.Add($"Status desconocido: '{fila.Status}'");

            if (errFila.Count > 0)
            {
                errores.Add($"[Orden OrderID={fila.OrderID}] {string.Join("; ", errFila)}");
                rechazados++;
                continue;
            }

            if (!vistos.Add(orderId))
            {
                rechazados++;
                continue;
            }

            try
            {
                await _db.ExecuteSpAsync("sp_InsertarOrden", cmd =>
                {
                    cmd.Parameters.AddWithValue("@IdOrden",    orderId);
                    cmd.Parameters.AddWithValue("@IdCliente",  clienteId);
                    cmd.Parameters.AddWithValue("@FechaOrden", fecha.Date);
                    cmd.Parameters.AddWithValue("@Status",     fila.Status.Trim());
                });
                insertados++;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                rechazados++;
            }
            catch (Exception ex)
            {
                errores.Add($"[Orden {orderId}] {ex.Message}");
                rechazados++;
            }
        }

        return new EtlResult("Ordenes", totales, insertados, rechazados, errores);
    }
}
