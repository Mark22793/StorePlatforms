namespace Storefront.Web.Models;

public class OrderDto
{
    public Guid OrderId { get; set; } // Pinalitan mula int patungong Guid
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Kasama ang listahan ng items para sa Order Status page
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public Guid OrderItemId { get; set; } // Pinalitan mula int patungong Guid
    public Guid OrderId { get; set; }     // Pinalitan mula int patungong Guid
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}