using ETL.Core.Models;
using ETL.Data.Helpers;
using Microsoft.Extensions.Logging;

namespace ETL.Data.Loaders;

public class AuditoriaService
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<AuditoriaService> _log;

    public AuditoriaService(DatabaseHelper db, ILogger<AuditoriaService> log)
        => (_db, _log) = (db, log);

    public async Task RegistrarAsync(AuditoriaETL auditoria)
    {
        try
        {
            await _db.ExecuteSpAsync("sp_RegistrarAuditoria", cmd =>
            {
                cmd.Parameters.AddWithValue("@TipoFuente",           auditoria.TipoFuente);
                cmd.Parameters.AddWithValue("@NombreArchivo",        auditoria.NombreArchivo);
                cmd.Parameters.AddWithValue("@RutaArchivo",          auditoria.RutaArchivo);
                cmd.Parameters.AddWithValue("@Extension",            auditoria.Extension);
                cmd.Parameters.AddWithValue("@RegistrosTotales",     auditoria.RegistrosTotales);
                cmd.Parameters.AddWithValue("@RegistrosInsertados",  auditoria.RegistrosInsertados);
                cmd.Parameters.AddWithValue("@RegistrosRechazados",  auditoria.RegistrosRechazados);
                cmd.Parameters.AddWithValue("@Estado",               auditoria.Estado);
                cmd.Parameters.AddWithValue("@MensajeError",
                    string.IsNullOrEmpty(auditoria.MensajeError)
                        ? DBNull.Value
                        : (object)auditoria.MensajeError);
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error registrando auditoría para {Archivo}", auditoria.NombreArchivo);
        }
    }
}
