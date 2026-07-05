using ETL.Core.DTOs;
using ETL.Core.Interfaces;
using ETL.Data.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ETL.Data.Loaders;

public class PaisesLoader : IEtlLoader
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<PaisesLoader> _log;

    public PaisesLoader(DatabaseHelper db, ILogger<PaisesLoader> log)
        => (_db, _log) = (db, log);

    public async Task<EtlResult> CargarAsync(string rutaArchivo)
    {
        var errores    = new List<string>();
        int insertados = 0, rechazados = 0;

        List<ClienteCsv> filas;
        try { filas = CsvReaderHelper.Leer<ClienteCsv>(rutaArchivo); }
        catch (Exception ex)
        {
            return new EtlResult("Paises", 0, 0, 0, [$"Error leyendo CSV: {ex.Message}"]);
        }

        var paisesUnicos = filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Country))
            .Select(f => f.Country.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet();

        int totales = paisesUnicos.Count;
        _log.LogInformation("Países únicos encontrados: {Count}", totales);

        foreach (var nombre in paisesUnicos)
        {
            try
            {
                await _db.ExecuteSpAsync("sp_InsertarPais", cmd =>
                {
                    cmd.Parameters.AddWithValue("@NombrePais", nombre);
                });
                insertados++;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                rechazados++;
            }
            catch (Exception ex)
            {
                errores.Add($"[País '{nombre}'] {ex.Message}");
                rechazados++;
            }
        }

        return new EtlResult("Paises", totales, insertados, rechazados, errores);
    }
}
