using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Api.Data;

namespace InventoryService.Api.Controllers;

[ApiController]
[Route("inventory/v1/[controller]")]
public class InventoriesController : ControllerBase
{
    private readonly InventoryDbContext _context;

    public InventoriesController(InventoryDbContext context)
    {
        _context = context;
    }

    // 1. GET ALL
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.Inventories.ToListAsync();
        return Ok(items);
    }

    // 2. GET BY PRODUCT ID
    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetByProductId(Guid productId)
    {
        var item = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item == null) return NotFound();
        return Ok(item);
    }

    // 3. CREATE STOCK ITEM
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] InventoryItem item)
    {
        item.LastUpdated = DateTime.UtcNow;
        _context.Inventories.Add(item);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetByProductId), new { productId = item.ProductId }, item);
    }

    // 4. UPDATE STOCK ITEM
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] InventoryItem updatedItem)
    {
        var item = await _context.Inventories.FindAsync(id);
        if (item == null) return NotFound();

        item.Quantity = updatedItem.Quantity;
        item.ReservedQuantity = updatedItem.ReservedQuantity;
        item.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // 5. DELETE STOCK ITEM
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.Inventories.FindAsync(id);
        if (item == null) return NotFound();

        _context.Inventories.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // 6. RESERVE STOCK (Saga Orchestration Step)
    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve([FromBody] ReserveStockRequest request)
    {
        var item = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == request.ProductId);
        if (item == null) return NotFound("Hindi nahanap ang produkto sa imbentaryo.");

        if (item.Quantity < request.Quantity)
        {
            return BadRequest("Kulang ang stock para sa order na ito.");
        }

        item.Quantity -= request.Quantity;
        item.ReservedQuantity += request.Quantity;
        item.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Stock reserved successfully.", item.ProductId, item.Quantity, item.ReservedQuantity });
    }

    // 7. RELEASE STOCK (Saga Compensation Path)
    [HttpPost("release")]
    public async Task<IActionResult> Release([FromBody] ReleaseStockRequest request)
    {
        var item = await _context.Inventories.FirstOrDefaultAsync(i => i.ProductId == request.ProductId);
        if (item == null) return NotFound("Hindi nahanap ang produkto sa imbentaryo.");

        item.Quantity += request.Quantity;
        if (item.ReservedQuantity >= request.Quantity)
        {
            item.ReservedQuantity -= request.Quantity;
        }
        item.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { Message = "Stock released successfully.", item.ProductId, item.Quantity, item.ReservedQuantity });
    }
}

public record ReserveStockRequest(Guid ProductId, int Quantity);
public record ReleaseStockRequest(Guid ProductId, int Quantity);