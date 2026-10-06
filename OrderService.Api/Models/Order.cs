namespace OrderService.Api.Models;

public class Order
{
    public Guid OrderId { get; set; } // Pinalitan mula int patungong Guid

    public string CustomerName { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
}