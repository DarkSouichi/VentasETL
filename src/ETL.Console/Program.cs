using ETL.Console;
using ETL.Data.Helpers;
using ETL.Data.Loaders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddSingleton<DatabaseHelper>();
services.AddSingleton<AuditoriaService>();
services.AddSingleton<CategoriasLoader>();
services.AddSingleton<PaisesLoader>();
services.AddSingleton<ProductosLoader>();
services.AddSingleton<ClientesLoader>();
services.AddSingleton<OrdenesLoader>();
services.AddSingleton<DetalleOrdenesLoader>();
services.AddSingleton<EtlOrchestrator>();
services.AddLogging(b => b
    .AddConsole()
    .SetMinimumLevel(LogLevel.Information));

var provider = services.BuildServiceProvider();

var etl = provider.GetRequiredService<EtlOrchestrator>();
await etl.EjecutarAsync();