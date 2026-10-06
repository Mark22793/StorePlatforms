using System.Text.Json.Serialization;

namespace OrderService.Api.Models;

public class OrderItem
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; } // Siguraduhing Guid din ito
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    [JsonIgnore]
    public Order? Order { get; set; }
}