namespace ETL.Core.Models;

public class Categoria
{
    public int IdCategoria { get; set; }
    public string NombreCategoria { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public bool Activo { get; set; } = true;
}

public class Pais
{
    public int IdPais { get; set; }
    public string NombrePais { get; set; } = string.Empty;
    public string? CodigoISO { get; set; }
}

public class EstadoOrden
{
    public int IdEstado { get; set; }
    public string NombreEstado { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public class Producto
{
    public int IdProducto { get; set; }
    public int IdCategoria { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string FuenteOrigen { get; set; } = "CSV";
    public DateTime FechaCarga { get; set; } = DateTime.Now;
    public bool Activo { get; set; } = true;
}

public class Cliente
{
    public int IdCliente { get; set; }
    public int IdPais { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Ciudad { get; set; }
    public string FuenteOrigen { get; set; } = "CSV";
    public DateTime FechaCarga { get; set; } = DateTime.Now;
    public bool Activo { get; set; } = true;
}

public class Orden
{
    public int IdOrden { get; set; }
    public int IdCliente { get; set; }
    public int IdEstado { get; set; }
    public DateTime FechaOrden { get; set; }
    public decimal TotalOrden { get; set; }
    public string FuenteOrigen { get; set; } = "CSV";
    public DateTime FechaCarga { get; set; } = DateTime.Now;
}

public class DetalleOrden
{
    public int IdOrden { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public DateTime FechaCarga { get; set; } = DateTime.Now;
}

public class AuditoriaETL
{
    public int IdFuente { get; set; }
    public string TipoFuente { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.Now;
    public int RegistrosTotales { get; set; }
    public int RegistrosInsertados { get; set; }
    public int RegistrosRechazados { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public string? MensajeError { get; set; }
}
