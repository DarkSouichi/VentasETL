using CsvHelper.Configuration.Attributes;

namespace ETL.Core.DTOs;

public class ProductoCsv
{
    [Name("ProductID")]    public string ProductID    { get; set; } = string.Empty;
    [Name("ProductName")]  public string ProductName  { get; set; } = string.Empty;
    [Name("Category")]     public string Category     { get; set; } = string.Empty;
    [Name("Price")]        public string Price        { get; set; } = string.Empty;
    [Name("Stock")]        public string Stock        { get; set; } = string.Empty;
}

public class ClienteCsv
{
    [Name("CustomerID")]  public string CustomerID  { get; set; } = string.Empty;
    [Name("FirstName")]   public string FirstName   { get; set; } = string.Empty;
    [Name("LastName")]    public string LastName    { get; set; } = string.Empty;
    [Name("Email")]       public string Email       { get; set; } = string.Empty;
    [Name("Phone")]       public string Phone       { get; set; } = string.Empty;
    [Name("City")]        public string City        { get; set; } = string.Empty;
    [Name("Country")]     public string Country     { get; set; } = string.Empty;
}

public class OrdenCsv
{
    [Name("OrderID")]    public string OrderID    { get; set; } = string.Empty;
    [Name("CustomerID")] public string CustomerID { get; set; } = string.Empty;
    [Name("OrderDate")]  public string OrderDate  { get; set; } = string.Empty;
    [Name("Status")]     public string Status     { get; set; } = string.Empty;
}

public class DetalleOrdenCsv
{
    [Name("OrderID")]    public string OrderID    { get; set; } = string.Empty;
    [Name("ProductID")]  public string ProductID  { get; set; } = string.Empty;
    [Name("Quantity")]   public string Quantity   { get; set; } = string.Empty;
    [Name("TotalPrice")] public string TotalPrice { get; set; } = string.Empty;
}
