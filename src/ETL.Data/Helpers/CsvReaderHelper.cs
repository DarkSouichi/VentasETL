using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ETL.Data.Helpers;

public static class CsvReaderHelper
{
    public static List<T> Leer<T>(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
            throw new FileNotFoundException($"Archivo no encontrado: {rutaArchivo}");

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,  
            BadDataFound = null,  
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(rutaArchivo);
        using var csv    = new CsvReader(reader, config);
        return csv.GetRecords<T>().ToList();
    }
}
