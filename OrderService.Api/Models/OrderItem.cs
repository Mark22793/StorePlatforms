using System.Text.Json.Serialization;

namespace OrderService.Api.Models;

public class OrderItem
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal { get; set; }

    [JsonIgnore]
    public Order Order { get; set; } = null!;
}