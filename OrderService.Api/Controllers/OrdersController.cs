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
    private readonly CatalogClient _catalogClient;
    private readonly RabbitMqPublisher _rabbitMqPublisher;

    public OrdersController(
        OrderDbContext context,
        CatalogClient catalogClient,
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

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> GetOrder(int id)
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
    public async Task<ActionResult<Order>> CreateOrder(
        CreateOrderRequest request)
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
            if (item.ProductId <= 0)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Order Item",
                    detail: "Product ID must be greater than zero."
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
            CustomerName = request.CustomerName,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            var product = await _catalogClient.GetProductAsync(item.ProductId);

            if (product == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Product Not Found",
                    detail: $"Product with ID {item.ProductId} was not found in Catalog Service."
                );
            }

            if (!product.IsActive)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Product Inactive",
                    detail: $"Product with ID {item.ProductId} is inactive."
                );
            }

            var subtotal = product.Price * item.Quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Subtotal = subtotal
            });

            order.TotalAmount += subtotal;
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

        await _rabbitMqPublisher.PublishOrderPlacedAsync(orderPlacedEvent);

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.OrderId },
            order);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrder(int id, Order order)
    {
        if (id != order.OrderId)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Order ID",
                detail: "The order ID in the URL does not match the product ID in the request body."
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

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrder(int id)
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