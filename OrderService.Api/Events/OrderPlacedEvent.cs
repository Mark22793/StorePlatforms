namespace OrderService.Api.Events;

public class OrderPlacedEvent
{
    public Guid OrderId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public List<OrderPlacedItem> Items { get; set; } = new();
}

public class OrderPlacedItem
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Subtotal { get; set; }
}
