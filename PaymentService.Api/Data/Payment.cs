namespace PaymentService.Api.Data;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded
    public string PaymentMethod { get; set; } = "CreditCard";
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? FailureReason { get; set; }
}