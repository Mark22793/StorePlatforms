namespace Shared.Events;

public record OrderPlacedEvent(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    decimal TotalAmount,
    string CorrelationId
);


public record PaymentFailedEvent(
    Guid OrderId,
    string Reason,
    string CorrelationId
);

public record ReleaseStockCommand(
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    string CorrelationId
);



public record ProcessPaymentCommand(
    Guid OrderId,
    decimal TotalAmount,
    string CorrelationId
);



public class PaymentProcessedEvent
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;

    public PaymentProcessedEvent() { }

    public PaymentProcessedEvent(Guid orderId, decimal amount, string transactionId, string correlationId)
    {
        OrderId = orderId;
        Amount = amount;
        TransactionId = transactionId;
        CorrelationId = correlationId;
    }
}


public class StockReservedEvent
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    public StockReservedEvent() { }

    public StockReservedEvent(Guid orderId, Guid productId, int quantity, decimal totalPrice, string correlationId)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        TotalPrice = totalPrice;
        CorrelationId = correlationId;
    }
}



public class InventoryFailedEvent
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;

    public InventoryFailedEvent() { }

    public InventoryFailedEvent(Guid orderId, string reason, string correlationId)
    {
        OrderId = orderId;
        Reason = reason;
        CorrelationId = correlationId;
    }
}