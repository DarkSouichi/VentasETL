# SalesETL – Sistema de Análisis de Ventas | Proceso ETL

## Requisitos
- .NET 8 SDK
- SQL Server Express (localhost\SQLEXPRESS)
- Visual Studio 2022
- Base de datos `SistemaVentasDB` creada

## Estructura del Proyecto
```
SalesETL/
├── src/
│   ├── ETL.Core/          # Modelos, interfaces, DTOs
│   ├── ETL.Data/          # Loaders, helpers de BD y CSV
│   └── ETL.Console/       # Punto de entrada, DI, orquestación
├── database/
│   ├── 01_create_database.sql          # De la Práctica 1
│   └── 02_stored_procedures_views.sql  # SPs, Vistas, Consultas
└── README.md
```

## Configuración antes de ejecutar

### 1. Copiar los CSV
Crear carpeta y copiar los 4 archivos:
```
C:\\BD_Ventas\\CSV\\products.csv
C:\\BD_Ventas\\CSV\\customers.csv
C:\\BD_Ventas\\CSV\\orders.csv
C:\\BD_Ventas\\CSV\\order_details.csv
```

### 2. Verificar appsettings.json
```json
"ConnectionStrings": {
  "SalesDB": "Server=localhost\\SQLEXPRESS;Database=SistemaVentasDB;Trusted_Connection=True;TrustServerCertificate=True;"
},
"CsvPaths": {
  "Productos":      "C:\\BD_Ventas\\CSV\\products.csv",
  ...
}
```

### 3. Ejecutar script SQL
En SSMS ejecutar el script que esta en el entregable de la asignacion.

### 4. Ejecutar la aplicación
```bash
cd src/ETL.Console
dotnet run
```

## Orden de carga (por dependencias FK)
1. Categorías (sin dependencias)
2. Países (sin dependencias)
3. Productos → requiere Categorías
4. Clientes → requiere Países
5. Órdenes → requiere Clientes + EstadosOrden
6. DetalleOrdenes → requiere Órdenes + Productos
