using ETL.Core.DTOs;
using ETL.Core.Interfaces;
using ETL.Data.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ETL.Data.Loaders;

public class ClientesLoader : IEtlLoader
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<ClientesLoader> _log;

    public ClientesLoader(DatabaseHelper db, ILogger<ClientesLoader> log)
        => (_db, _log) = (db, log);

    public async Task<EtlResult> CargarAsync(string rutaArchivo)
    {
        var errores    = new List<string>();
        int insertados = 0, rechazados = 0;

        List<ClienteCsv> filas;
        try { filas = CsvReaderHelper.Leer<ClienteCsv>(rutaArchivo); }
        catch (Exception ex)
        {
            return new EtlResult("Clientes", 0, 0, 0, [$"Error leyendo CSV: {ex.Message}"]);
        }

        var vistos  = new HashSet<int>();
        var emails  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totales = filas.Count;

        foreach (var fila in filas)
        {
            var errFila = new List<string>();

            if (string.IsNullOrWhiteSpace(fila.CustomerID)) errFila.Add("CustomerID vacío");
            if (string.IsNullOrWhiteSpace(fila.FirstName))  errFila.Add("FirstName vacío");
            if (string.IsNullOrWhiteSpace(fila.LastName))   errFila.Add("LastName vacío");
            if (string.IsNullOrWhiteSpace(fila.Email))      errFila.Add("Email vacío");
            if (string.IsNullOrWhiteSpace(fila.Country))    errFila.Add("Country vacío");

            if (!int.TryParse(fila.CustomerID, out int id))
                errFila.Add($"CustomerID no es entero: '{fila.CustomerID}'");

            // Validar formato de email
            if (!string.IsNullOrWhiteSpace(fila.Email) && !fila.Email.Contains('@'))
                errFila.Add($"Email inválido: '{fila.Email}'");

            if (errFila.Count > 0)
            {
                errores.Add($"[Cliente CustomerID={fila.CustomerID}] {string.Join("; ", errFila)}");
                rechazados++;
                continue;
            }

            // Duplicado por ID
            if (!vistos.Add(id))
            {
                rechazados++;
                continue;
            }

            // Duplicado por Email en memoria
            if (!emails.Add(fila.Email.Trim()))
            {
                errores.Add($"[Cliente {id}] Email duplicado: {fila.Email}");
                rechazados++;
                continue;
            }

            try
            {
                await _db.ExecuteSpAsync("sp_InsertarCliente", cmd =>
                {
                    cmd.Parameters.AddWithValue("@IdCliente", id);
                    cmd.Parameters.AddWithValue("@Nombre",    fila.FirstName.Trim());
                    cmd.Parameters.AddWithValue("@Apellido",  fila.LastName.Trim());
                    cmd.Parameters.AddWithValue("@Email",     fila.Email.Trim());
                    cmd.Parameters.AddWithValue("@Telefono",  string.IsNullOrWhiteSpace(fila.Phone)  ? DBNull.Value : (object)fila.Phone.Trim());
                    cmd.Parameters.AddWithValue("@Ciudad",    string.IsNullOrWhiteSpace(fila.City)   ? DBNull.Value : (object)fila.City.Trim());
                    cmd.Parameters.AddWithValue("@Pais",      fila.Country.Trim());
                });
                insertados++;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                rechazados++;
            }
            catch (Exception ex)
            {
                errores.Add($"[Cliente {id}] {ex.Message}");
                rechazados++;
            }
        }

        return new EtlResult("Clientes", totales, insertados, rechazados, errores);
    }
}
