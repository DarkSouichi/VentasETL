namespace ETL.Core.Interfaces;

public record EtlResult(
    string Entidad,
    int Totales,
    int Insertados,
    int Rechazados,
    List<string> Errores
);

public interface IEtlLoader
{
    Task<EtlResult> CargarAsync(string rutaArchivo);
}
