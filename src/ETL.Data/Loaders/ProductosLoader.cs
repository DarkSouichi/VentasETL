using ETL.Core.DTOs;
using ETL.Core.Interfaces;
using ETL.Data.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ETL.Data.Loaders;

public class ProductosLoader : IEtlLoader
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<ProductosLoader> _log;

    public ProductosLoader(DatabaseHelper db, ILogger<ProductosLoader> log)
        => (_db, _log) = (db, log);

    public async Task<EtlResult> CargarAsync(string rutaArchivo)
    {
        var errores    = new List<string>();
        int insertados = 0, rechazados = 0;

        List<ProductoCsv> filas;
        try { filas = CsvReaderHelper.Leer<ProductoCsv>(rutaArchivo); }
        catch (Exception ex)
        {
            return new EtlResult("Productos", 0, 0, 0, [$"Error leyendo CSV: {ex.Message}"]);
        }

        var vistos   = new HashSet<int>();
        int totales  = filas.Count;

        foreach (var fila in filas)
        {
            var errFila = new List<string>();

            if (string.IsNullOrWhiteSpace(fila.ProductID))   errFila.Add("ProductID vacío");
            if (string.IsNullOrWhiteSpace(fila.ProductName)) errFila.Add("ProductName vacío");
            if (string.IsNullOrWhiteSpace(fila.Category))    errFila.Add("Category vacío");

            if (!int.TryParse(fila.ProductID, out int id))
                errFila.Add($"ProductID no es entero: '{fila.ProductID}'");

            if (!decimal.TryParse(fila.Price,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal precio) || precio < 0)
                errFila.Add($"Price inválido: '{fila.Price}'");

            if (!int.TryParse(fila.Stock, out int stock) || stock < 0)
                errFila.Add($"Stock inválido: '{fila.Stock}'");

            if (errFila.Count > 0)
            {
                errores.Add($"[Producto fila ProductID={fila.ProductID}] {string.Join("; ", errFila)}");
                rechazados++;
                continue;
            }

            if (!vistos.Add(id))
            {
                errores.Add($"[Producto {id}] Duplicado en CSV, omitido.");
                rechazados++;
                continue;
            }

            try
            {
                await _db.ExecuteSpAsync("sp_InsertarProducto", cmd =>
                {
                    cmd.Parameters.AddWithValue("@IdProducto",     id);
                    cmd.Parameters.AddWithValue("@NombreProducto", fila.ProductName.Trim());
                    cmd.Parameters.AddWithValue("@Categoria",      fila.Category.Trim());
                    cmd.Parameters.AddWithValue("@Precio",         precio);
                    cmd.Parameters.AddWithValue("@Stock",          stock);
                });
                insertados++;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                rechazados++;
            }
            catch (Exception ex)
            {
                errores.Add($"[Producto {id}] {ex.Message}");
                rechazados++;
            }
        }

        return new EtlResult("Productos", totales, insertados, rechazados, errores);
    }
}
