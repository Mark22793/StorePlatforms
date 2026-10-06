using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Api.Data;
using OrderService.Api.Events;
using OrderService.Api.Models;
using OrderService.Api.Services;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("orders/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly ICatalogClient _catalogClient;
    private readonly RabbitMqPublisher _rabbitMqPublisher;

    public OrdersController(
        OrderDbContext context,
        ICatalogClient catalogClient,
        RabbitMqPublisher rabbitMqPublisher)
    {
        _context = context;
        _catalogClient = catalogClient;
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
    {
        var orders = await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync();

        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> GetOrder(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order Not Found",
                detail: $"Order with ID {id} was not found."
            );
        }

        return Ok(order);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> CreateOrder(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Order",
                detail: "Customer name is required."
            );
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Order",
                detail: "At least one order item is required."
            );
        }

        foreach (var item in request.Items)
        {
            if (item.ProductId == Guid.Empty)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Order Item",
                    detail: "Valid Product ID (Guid) is required."
                );
            }

            if (item.Quantity <= 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Order Item",
                    detail: "Quantity must be greater than zero."
                );
            }
        }

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            CustomerName = request.CustomerName,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            try
            {
                var product = await _catalogClient.GetProductByIdAsync(item.ProductId);

                if (product == null)
                {
                    return Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Product Not Found",
                        detail: $"Product with ID ({item.ProductId}) was not found in Catalog Service."
                    );
                }

                var unitPrice = (decimal)product.Price;
                var subtotal = unitPrice * item.Quantity;

                order.Items.Add(new OrderItem
                {
                    OrderItemId = Guid.NewGuid(),
                    OrderId = order.OrderId,
                    ProductId = product.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice,
                    Subtotal = subtotal
                });

                order.TotalAmount += subtotal;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Product Not Found",
                    detail: $"Product with ID ({item.ProductId}) was not found in Catalog Service."
                );
            }
            catch (Exception ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Catalog Request Failed",
                    detail: ex.Message
                );
            }
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var correlationId =
            HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        var orderPlacedEvent = new OrderPlacedEvent
        {
            OrderId = order.OrderId,
            CustomerName = order.CustomerName,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,
            CorrelationId = correlationId,
            Items = order.Items.Select(item => new OrderPlacedItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.Subtotal
            }).ToList()
        };

        try
        {
            await _rabbitMqPublisher.PublishOrderPlacedAsync(orderPlacedEvent);
        }
        catch (Exception ex)
        {
            // The order is already saved; it stays Pending until the saga can run
            HttpContext.RequestServices.GetRequiredService<ILogger<OrdersController>>()
                .LogError(ex, "Failed to publish OrderPlaced event for order {OrderId}", order.OrderId);
        }

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.OrderId },
            order);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrder(Guid id, Order order)
    {
        if (id != order.OrderId)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Order ID",
                detail: "The order ID in the URL does not match the body."
            );
        }

        var existingOrder = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (existingOrder == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order Not Found",
                detail: $"Order with ID {id} was not found."
            );
        }

        existingOrder.CustomerName = order.CustomerName;
        existingOrder.Status = order.Status;
        existingOrder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrder(Guid id)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Order Not Found",
                detail: $"Order with ID {id} was not found."
            );
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}