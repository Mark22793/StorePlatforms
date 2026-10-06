using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Data;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("payment/v1/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentDbContext _context;

    public PaymentsController(PaymentDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var payments = await _context.Payments.ToListAsync();
        return Ok(payments);
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        if (payment == null) return NotFound();
        return Ok(payment);
    }

    // Process Payment Step (Saga Step)
    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        bool isSuccess = request.Amount <= 100000;

        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? "CreditCard" : request.PaymentMethod,
            Status = isSuccess ? "Completed" : "Failed",
            FailureReason = isSuccess ? null : "Amount exceeds max limit of 100,000",
            TransactionDate = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        if (!isSuccess)
        {
            return BadRequest(new { Message = "Payment failed.", payment });
        }

        return Ok(new { Message = "Payment processed successfully.", payment });
    }

    // Refund Step (Saga Compensation Path)
    [HttpPost("refund")]
    public async Task<IActionResult> RefundPayment([FromBody] RefundPaymentRequest request)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == request.OrderId);
        if (payment == null) return NotFound("Payment record not found.");

        payment.Status = "Refunded";
        payment.TransactionDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Payment refunded successfully.", payment });
    }
}

public record ProcessPaymentRequest(Guid OrderId, decimal Amount, string PaymentMethod = "CreditCard");
public record RefundPaymentRequest(Guid OrderId);