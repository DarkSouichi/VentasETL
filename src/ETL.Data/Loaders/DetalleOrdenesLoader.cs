using ETL.Core.DTOs;
using ETL.Core.Interfaces;
using ETL.Data.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ETL.Data.Loaders;

public class DetalleOrdenesLoader : IEtlLoader
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<DetalleOrdenesLoader> _log;

    public DetalleOrdenesLoader(DatabaseHelper db, ILogger<DetalleOrdenesLoader> log)
        => (_db, _log) = (db, log);

    public async Task<EtlResult> CargarAsync(string rutaArchivo)
    {
        var errores    = new List<string>();
        int insertados = 0, rechazados = 0;

        List<DetalleOrdenCsv> filas;
        try { filas = CsvReaderHelper.Leer<DetalleOrdenCsv>(rutaArchivo); }
        catch (Exception ex)
        {
            return new EtlResult("DetalleOrdenes", 0, 0, 0, [$"Error leyendo CSV: {ex.Message}"]);
        }

        int totales = filas.Count;

        const int LOTE = 500;
        var lote        = new List<(int orderId, int prodId, int qty, decimal precio)>(LOTE);

        async Task FlushLoteAsync()
        {
            foreach (var (orderId, prodId, qty, precio) in lote)
            {
                try
                {
                    await _db.ExecuteSpAsync("sp_InsertarDetalleOrden", cmd =>
                    {
                        cmd.Parameters.AddWithValue("@IdOrden",        orderId);
                        cmd.Parameters.AddWithValue("@IdProducto",     prodId);
                        cmd.Parameters.AddWithValue("@Cantidad",       qty);
                        cmd.Parameters.AddWithValue("@PrecioUnitario", precio);
                    });
                    insertados++;
                }
                catch (SqlException ex) when (ex.Number == 547)
                {
                    errores.Add($"[Detalle Orden={orderId} Prod={prodId}] FK inválida: {ex.Message}");
                    rechazados++;
                }
                catch (Exception ex)
                {
                    errores.Add($"[Detalle Orden={orderId} Prod={prodId}] {ex.Message}");
                    rechazados++;
                }
            }
            lote.Clear();
        }

        int fila_num = 0;
        foreach (var fila in filas)
        {
            fila_num++;
            var errFila = new List<string>();

            if (string.IsNullOrWhiteSpace(fila.OrderID))   errFila.Add("OrderID vacío");
            if (string.IsNullOrWhiteSpace(fila.ProductID)) errFila.Add("ProductID vacío");
            if (string.IsNullOrWhiteSpace(fila.Quantity))  errFila.Add("Quantity vacío");
            if (string.IsNullOrWhiteSpace(fila.TotalPrice))errFila.Add("TotalPrice vacío");

            if (!int.TryParse(fila.OrderID,   out int orderId))  errFila.Add("OrderID no entero");
            if (!int.TryParse(fila.ProductID,  out int prodId))   errFila.Add("ProductID no entero");
            if (!int.TryParse(fila.Quantity,   out int qty) || qty <= 0) errFila.Add("Quantity inválido");
            if (!decimal.TryParse(fila.TotalPrice,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal total) || total < 0)
                errFila.Add("TotalPrice inválido");

            if (errFila.Count > 0)
            {
                errores.Add($"[DetalleOrden fila {fila_num}] {string.Join("; ", errFila)}");
                rechazados++;
                continue;
            }

            decimal precioUnit = qty > 0 ? Math.Round(total / qty, 2) : 0;

            lote.Add((orderId, prodId, qty, precioUnit));

            if (lote.Count >= LOTE)
            {
                await FlushLoteAsync();
                _log.LogInformation("Progreso DetalleOrdenes: {Ins}/{Tot}", insertados, totales);
            }
        }

        if (lote.Count > 0) await FlushLoteAsync();

        return new EtlResult("DetalleOrdenes", totales, insertados, rechazados, errores);
    }
}
