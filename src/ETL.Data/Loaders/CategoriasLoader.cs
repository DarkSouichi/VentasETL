using ETL.Core.DTOs;
using ETL.Core.Interfaces;
using ETL.Data.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ETL.Data.Loaders;

public class CategoriasLoader : IEtlLoader
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<CategoriasLoader> _log;

    public CategoriasLoader(DatabaseHelper db, ILogger<CategoriasLoader> log)
    {
        _db  = db;
        _log = log;
    }

    public async Task<EtlResult> CargarAsync(string rutaArchivo)
    {
        var errores    = new List<string>();
        int insertados = 0, rechazados = 0;

        // EXTRACCION
        List<ProductoCsv> filas;
        try { filas = CsvReaderHelper.Leer<ProductoCsv>(rutaArchivo); }
        catch (Exception ex)
        {
            return new EtlResult("Categorias", 0, 0, 0,
                [$"Error leyendo CSV: {ex.Message}"]);
        }

        // TRANSFORMACION: se obtienen categorias unicas
        var categoriasUnicas = filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Category))
            .Select(f => f.Category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet();

        int totales = categoriasUnicas.Count;
        _log.LogInformation("Categorías únicas encontradas: {Count}", totales);

        // CARGA con Stored Procedure
        foreach (var nombre in categoriasUnicas)
        {
            try
            {
                await _db.ExecuteSpAsync("sp_InsertarCategoria", cmd =>
                {
                    cmd.Parameters.AddWithValue("@NombreCategoria", nombre);
                });
                insertados++;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                // Duplicado
                _log.LogDebug("Categoría duplicada omitida: {Nombre}", nombre);
                rechazados++;
            }
            catch (Exception ex)
            {
                errores.Add($"[Categoria '{nombre}'] {ex.Message}");
                rechazados++;
            }
        }

        return new EtlResult("Categorias", totales, insertados, rechazados, errores);
    }
}
