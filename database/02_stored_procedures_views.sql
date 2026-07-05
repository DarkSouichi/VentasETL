-- ============================================================
-- SISTEMA DE ANÁLISIS DE VENTAS – ETL
-- Script 2: Stored Procedures, Vistas y Consultas de Validación
-- Base de datos: SistemaVentasDB
-- ============================================================

USE SistemaVentasDB;
GO

-- ============================================================
-- SECCIÓN 1: TABLA DE AUDITORÍA ETL
-- (Agrega columnas extra a FuentesDatos para el requerimiento)
-- ============================================================

-- Si ya existe FuentesDatos del script anterior, la ampliamos
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('FuentesDatos')
      AND name = 'RutaArchivo'
)
BEGIN
    ALTER TABLE FuentesDatos ADD
        RutaArchivo         NVARCHAR(500)   NULL,
        Extension           NVARCHAR(20)    NULL,
        RegistrosTotales    INT             NOT NULL DEFAULT 0,
        RegistrosInsertados INT             NOT NULL DEFAULT 0,
        RegistrosRechazados INT             NOT NULL DEFAULT 0;
END
GO


-- ============================================================
-- SECCIÓN 2: STORED PROCEDURES DE CARGA
-- ============================================================

-- ----------------------------------------------------------
-- SP: sp_InsertarCategoria
-- Inserta una categoría si no existe (por nombre)
-- ----------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_InsertarCategoria
    @NombreCategoria NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (
        SELECT 1 FROM Categorias
        WHERE NombreCategoria = @NombreCategoria
    )
    BEGIN
        INSERT INTO Categorias (NombreCategoria, FechaCreacion, Activo)
        VALUES (@NombreCategoria, GETDATE(), 1);
    END
END
GO

-- ----------------------------------------------------------
-- SP: sp_InsertarPais
-- Inserta un país si no existe (por nombre)
-- ----------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_InsertarPais
    @NombrePais NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (
        SELECT 1 FROM Paises WHERE NombrePais = @NombrePais
    )
    BEGIN
        INSERT INTO Paises (NombrePais)
        VALUES (@NombrePais);
    END
END
GO

-- ----------------------------------------------------------
-- SP: sp_InsertarProducto
-- Inserta un producto resolviendo FK de Categorias
-- ----------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_InsertarProducto
    @IdProducto      INT,
    @NombreProducto  NVARCHAR(200),
    @Categoria       NVARCHAR(100),
    @Precio          DECIMAL(10,2),
    @Stock           INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Validaciones
    IF @Precio < 0
    BEGIN
        RAISERROR('Precio no puede ser negativo.', 16, 1);
        RETURN;
    END
    IF @Stock < 0
    BEGIN
        RAISERROR('Stock no puede ser negativo.', 16, 1);
        RETURN;
    END

    -- Resolver FK: obtener IdCategoria
    DECLARE @IdCategoria INT;
    SELECT @IdCategoria = IdCategoria
    FROM Categorias
    WHERE NombreCategoria = @Categoria;

    IF @IdCategoria IS NULL
    BEGIN
        RAISERROR('Categoría no encontrada: %s', 16, 1, @Categoria);
        RETURN;
    END

    -- Insertar solo si no existe
    IF NOT EXISTS (SELECT 1 FROM Productos WHERE IdProducto = @IdProducto)
    BEGIN
        INSERT INTO Productos
            (IdProducto, IdCategoria, NombreProducto, Precio, Stock, FuenteOrigen, FechaCarga, Activo)
        VALUES
            (@IdProducto, @IdCategoria, @NombreProducto, @Precio, @Stock, 'CSV', GETDATE(), 1);
    END
END
GO

-- ----------------------------------------------------------
-- SP: sp_InsertarCliente
-- Inserta un cliente resolviendo FK de Paises
-- ----------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_InsertarCliente
    @IdCliente  INT,
    @Nombre     NVARCHAR(100),
    @Apellido   NVARCHAR(100),
    @Email      NVARCHAR(200),
    @Telefono   NVARCHAR(50)  = NULL,
    @Ciudad     NVARCHAR(150) = NULL,
    @Pais       NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;

    -- Resolver FK: obtener IdPais
    DECLARE @IdPais INT;
    SELECT @IdPais = IdPais FROM Paises WHERE NombrePais = @Pais;

    IF @IdPais IS NULL
    BEGIN
        RAISERROR('País no encontrado: %s', 16, 1, @Pais);
        RETURN;
    END

    -- Insertar solo si no existe (por ID o Email)
    IF NOT EXISTS (
        SELECT 1 FROM Clientes
        WHERE IdCliente = @IdCliente OR Email = @Email
    )
    BEGIN
        INSERT INTO Clientes
            (IdCliente, IdPais, Nombre, Apellido, Email, Telefono, Ciudad,
             FuenteOrigen, FechaCarga, Activo)
        VALUES
            (@IdCliente, @IdPais, @Nombre, @Apellido, @Email, @Telefono, @Ciudad,
             'CSV', GETDATE(), 1);
    END
END
GO

-- ----------------------------------------------------------
-- SP: sp_InsertarOrden
-- Inserta una orden resolviendo FK de Clientes y EstadosOrden
-- ----------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_InsertarOrden
    @IdOrden    INT,
    @IdCliente  INT,
    @FechaOrden DATE,
    @Status     NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- Resolver FK: Estado
    DECLARE @IdEstado INT;
    SELECT @IdEstado = IdEstado FROM EstadosOrden WHERE NombreEstado = @Status;

    IF @IdEstado IS NULL
    BEGIN
        RAISERROR('Estado no encontrado: %s', 16, 1, @Status);
        RETURN;
    END

    -- Validar integridad referencial: Cliente debe existir
    IF NOT EXISTS (SELECT 1 FROM Clientes WHERE IdCliente = @IdCliente)
    BEGIN
        RAISERROR('Cliente %d no existe.', 16, 1, @IdCliente);
        RETURN;
    END

    -- Insertar si no existe
    IF NOT EXISTS (SELECT 1 FROM Ordenes WHERE IdOrden = @IdOrden)
    BEGIN
        INSERT INTO Ordenes
            (IdOrden, IdCliente, IdEstado, FechaOrden, TotalOrden, FuenteOrigen, FechaCarga)
        VALUES
            (@IdOrden, @IdCliente, @IdEstado, @FechaOrden, 0, 'CSV', GETDATE());
    END
END
GO

-- ----------------------------------------------------------
-- SP: sp_InsertarDetalleOrden
-- Inserta una línea de detalle y actualiza TotalOrden
-- ----------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_InsertarDetalleOrden
    @IdOrden        INT,
    @IdProducto     INT,
    @Cantidad       INT,
    @PrecioUnitario DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;

    -- Validaciones
    IF @Cantidad <= 0
    BEGIN
        RAISERROR('Cantidad debe ser mayor a 0.', 16, 1);
        RETURN;
    END
    IF @PrecioUnitario < 0
    BEGIN
        RAISERROR('PrecioUnitario no puede ser negativo.', 16, 1);
        RETURN;
    END

    -- Validar integridad referencial
    IF NOT EXISTS (SELECT 1 FROM Ordenes   WHERE IdOrden   = @IdOrden)
    BEGIN
        RAISERROR('Orden %d no existe.', 16, 1, @IdOrden);
        RETURN;
    END
    IF NOT EXISTS (SELECT 1 FROM Productos WHERE IdProducto = @IdProducto)
    BEGIN
        RAISERROR('Producto %d no existe.', 16, 1, @IdProducto);
        RETURN;
    END

    -- Insertar detalle (TotalLinea es columna calculada)
    INSERT INTO DetalleOrdenes (IdOrden, IdProducto, Cantidad, PrecioUnitario, FechaCarga)
    VALUES (@IdOrden, @IdProducto, @Cantidad, @PrecioUnitario, GETDATE());

    -- Actualizar TotalOrden en cabecera
    UPDATE Ordenes
    SET TotalOrden = (
        SELECT ISNULL(SUM(TotalLinea), 0)
        FROM DetalleOrdenes
        WHERE IdOrden = @IdOrden
    )
    WHERE IdOrden = @IdOrden;
END
GO

-- ----------------------------------------------------------
-- SP: sp_RegistrarAuditoria
-- Registra el resultado de cada carga ETL
-- ----------------------------------------------------------
CREATE OR ALTER PROCEDURE sp_RegistrarAuditoria
    @TipoFuente           NVARCHAR(50),
    @NombreArchivo        NVARCHAR(200),
    @RutaArchivo          NVARCHAR(500),
    @Extension            NVARCHAR(20),
    @RegistrosTotales     INT,
    @RegistrosInsertados  INT,
    @RegistrosRechazados  INT,
    @Estado               NVARCHAR(20),
    @MensajeError         NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO FuentesDatos
        (TipoFuente, NombreArchivo, RutaArchivo, Extension,
         FechaCarga, RegistrosTotales, RegistrosInsertados, RegistrosRechazados,
         RegistrosCargados, RegistrosRechazados, Estado, MensajeError)
    VALUES
        (@TipoFuente, @NombreArchivo, @RutaArchivo, @Extension,
         GETDATE(), @RegistrosTotales, @RegistrosInsertados, @RegistrosRechazados,
         @RegistrosInsertados, @RegistrosRechazados, @Estado, @MensajeError);
END
GO


-- ============================================================
-- SECCIÓN 3: VISTAS ANALÍTICAS (KPIs)
-- ============================================================

-- Vista 1: Total de ventas por producto
CREATE OR ALTER VIEW vw_VentasPorProducto AS
SELECT
    p.IdProducto,
    p.NombreProducto,
    c.NombreCategoria,
    SUM(d.Cantidad)            AS TotalUnidades,
    SUM(d.TotalLinea)          AS TotalVentas,
    COUNT(DISTINCT d.IdOrden)  AS NumOrdenes
FROM DetalleOrdenes d
JOIN Productos   p ON d.IdProducto  = p.IdProducto
JOIN Categorias  c ON p.IdCategoria = c.IdCategoria
JOIN Ordenes     o ON d.IdOrden     = o.IdOrden
WHERE o.IdEstado <> (SELECT IdEstado FROM EstadosOrden WHERE NombreEstado = 'Cancelled')
GROUP BY p.IdProducto, p.NombreProducto, c.NombreCategoria;
GO

-- Vista 2: Total de ventas por cliente
CREATE OR ALTER VIEW vw_VentasPorCliente AS
SELECT
    cl.IdCliente,
    cl.Nombre + ' ' + cl.Apellido  AS NombreCompleto,
    cl.Email,
    pa.NombrePais,
    COUNT(DISTINCT o.IdOrden)       AS TotalOrdenes,
    SUM(o.TotalOrden)               AS TotalCompras
FROM Clientes  cl
JOIN Paises    pa ON cl.IdPais    = pa.IdPais
JOIN Ordenes   o  ON cl.IdCliente = o.IdCliente
WHERE o.IdEstado <> (SELECT IdEstado FROM EstadosOrden WHERE NombreEstado = 'Cancelled')
GROUP BY cl.IdCliente, cl.Nombre, cl.Apellido, cl.Email, pa.NombrePais;
GO

-- Vista 3: Total de ventas por mes
CREATE OR ALTER VIEW vw_VentasPorMes AS
SELECT
    AñoOrden,
    MesOrden,
    DATENAME(MONTH, DATEFROMPARTS(AñoOrden, MesOrden, 1)) AS NombreMes,
    COUNT(DISTINCT IdOrden)  AS TotalOrdenes,
    SUM(TotalOrden)          AS TotalVentas
FROM Ordenes
WHERE IdEstado <> (SELECT IdEstado FROM EstadosOrden WHERE NombreEstado = 'Cancelled')
GROUP BY AñoOrden, MesOrden;
GO

-- Vista 4: Top 5 productos más vendidos
CREATE OR ALTER VIEW vw_Top5Productos AS
SELECT TOP 5
    p.IdProducto,
    p.NombreProducto,
    c.NombreCategoria,
    SUM(d.Cantidad)   AS TotalUnidades,
    SUM(d.TotalLinea) AS TotalVentas
FROM DetalleOrdenes d
JOIN Productos  p ON d.IdProducto  = p.IdProducto
JOIN Categorias c ON p.IdCategoria = c.IdCategoria
JOIN Ordenes    o ON d.IdOrden     = o.IdOrden
WHERE o.IdEstado <> (SELECT IdEstado FROM EstadosOrden WHERE NombreEstado = 'Cancelled')
GROUP BY p.IdProducto, p.NombreProducto, c.NombreCategoria
ORDER BY TotalVentas DESC;
GO

-- Vista 5: Top 5 clientes con más compras
CREATE OR ALTER VIEW vw_Top5Clientes AS
SELECT TOP 5
    cl.IdCliente,
    cl.Nombre + ' ' + cl.Apellido AS NombreCompleto,
    cl.Email,
    COUNT(DISTINCT o.IdOrden)     AS TotalOrdenes,
    SUM(o.TotalOrden)             AS TotalCompras
FROM Clientes  cl
JOIN Ordenes   o ON cl.IdCliente = o.IdCliente
WHERE o.IdEstado <> (SELECT IdEstado FROM EstadosOrden WHERE NombreEstado = 'Cancelled')
GROUP BY cl.IdCliente, cl.Nombre, cl.Apellido, cl.Email
ORDER BY TotalCompras DESC;
GO

-- Vista 6: Resumen de auditoría ETL
CREATE OR ALTER VIEW vw_AuditoriaETL AS
SELECT
    IdFuente,
    TipoFuente,
    NombreArchivo,
    FechaCarga,
    RegistrosTotales,
    RegistrosInsertados,
    RegistrosRechazados,
    Estado,
    MensajeError
FROM FuentesDatos
WHERE RegistrosTotales IS NOT NULL;
GO


-- ============================================================
-- SECCIÓN 4: CINCO CONSULTAS DE VALIDACIÓN
-- ============================================================

-- ── CONSULTA 1 ──────────────────────────────────────────────
-- Verifica conteo de registros en cada tabla principal
PRINT '=== CONSULTA 1: Conteo de registros por tabla ===';
SELECT 'Categorias'     AS Tabla, COUNT(*) AS Registros FROM Categorias
UNION ALL
SELECT 'Paises',               COUNT(*) FROM Paises
UNION ALL
SELECT 'EstadosOrden',         COUNT(*) FROM EstadosOrden
UNION ALL
SELECT 'Productos',            COUNT(*) FROM Productos
UNION ALL
SELECT 'Clientes',             COUNT(*) FROM Clientes
UNION ALL
SELECT 'Ordenes',              COUNT(*) FROM Ordenes
UNION ALL
SELECT 'DetalleOrdenes',       COUNT(*) FROM DetalleOrdenes
ORDER BY Registros DESC;
GO

-- ── CONSULTA 2 ──────────────────────────────────────────────
-- Verifica integridad referencial: detalles sin orden padre
PRINT '=== CONSULTA 2: Integridad referencial - Detalles sin Orden ===';
SELECT COUNT(*) AS DetallesSinOrden
FROM DetalleOrdenes d
WHERE NOT EXISTS (
    SELECT 1 FROM Ordenes o WHERE o.IdOrden = d.IdOrden
);
-- Resultado esperado: 0
GO

-- ── CONSULTA 3 ──────────────────────────────────────────────
-- Verifica integridad referencial: órdenes sin cliente
PRINT '=== CONSULTA 3: Integridad referencial - Órdenes sin Cliente ===';
SELECT COUNT(*) AS OrdenesSinCliente
FROM Ordenes o
WHERE NOT EXISTS (
    SELECT 1 FROM Clientes c WHERE c.IdCliente = o.IdCliente
);
-- Resultado esperado: 0
GO

-- ── CONSULTA 4 ──────────────────────────────────────────────
-- Verifica que TotalOrden coincide con la suma de DetalleOrdenes
PRINT '=== CONSULTA 4: Consistencia de totales (muestra top 10 diferencias) ===';
SELECT TOP 10
    o.IdOrden,
    o.TotalOrden                                AS TotalEnCabecera,
    ISNULL(SUM(d.TotalLinea), 0)               AS TotalCalculado,
    o.TotalOrden - ISNULL(SUM(d.TotalLinea),0) AS Diferencia
FROM Ordenes o
LEFT JOIN DetalleOrdenes d ON o.IdOrden = d.IdOrden
GROUP BY o.IdOrden, o.TotalOrden
HAVING ABS(o.TotalOrden - ISNULL(SUM(d.TotalLinea), 0)) > 0.01
ORDER BY ABS(o.TotalOrden - ISNULL(SUM(d.TotalLinea), 0)) DESC;
-- Resultado esperado: 0 filas
GO

-- ── CONSULTA 5 ──────────────────────────────────────────────
-- Verifica que no existan emails duplicados en Clientes
PRINT '=== CONSULTA 5: Emails duplicados en Clientes ===';
SELECT Email, COUNT(*) AS Ocurrencias
FROM Clientes
GROUP BY Email
HAVING COUNT(*) > 1;
-- Resultado esperado: 0 filas
GO

-- ── CONSULTA 6 (BONUS) ──────────────────────────────────────
-- Muestra Top 5 productos más vendidos
PRINT '=== CONSULTA 6: Top 5 productos más vendidos ===';
SELECT * FROM vw_Top5Productos;
GO

-- ── CONSULTA 7 (BONUS) ──────────────────────────────────────
-- Muestra resumen de auditoría ETL
PRINT '=== CONSULTA 7: Auditoría del proceso ETL ===';
SELECT
    NombreArchivo,
    FechaCarga,
    RegistrosTotales,
    RegistrosInsertados,
    RegistrosRechazados,
    Estado
FROM vw_AuditoriaETL
ORDER BY FechaCarga DESC;
GO

PRINT 'Script ejecutado correctamente.';
GO
