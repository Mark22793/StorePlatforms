using CatalogService.Api;
using CatalogService.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Controllers;

[ApiController]
[Route("catalog/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly CatalogDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        CatalogDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<ProductsController> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // GET: /catalog/v1/products
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        var products = await _context.Products
            .AsNoTracking()
            .ToListAsync();

        return Ok(products);
    }

    // GET: /catalog/v1/products/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Product>> GetProduct(Guid id)
    {
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: $"Product with ID {id} was not found."
            );
        }

        return Ok(product);
    }

    // POST: /catalog/v1/products
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Product>> CreateProduct(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.Name))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Product",
                detail: "Product name is required."
            );
        }

        if (product.Price < 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Product",
                detail: "Product price cannot be negative."
            );
        }

        if (product.StockQuantity < 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Product",
                detail: "Stock quantity cannot be negative."
            );
        }

        if (product.ProductId == Guid.Empty)
        {
            product.ProductId = Guid.NewGuid();
        }

        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // --- AUTOMATIC SYNC TO INVENTORY SERVICE (PORT 7015) ---
        try
        {
            var client = _httpClientFactory.CreateClient();
            var inventoryUrl = "https://localhost:7015/inventory/v1/Inventories";

            var inventoryPayload = new
            {
                Id = Guid.NewGuid(),
                ProductId = product.ProductId,
                Quantity = product.StockQuantity,
                ReservedQuantity = 0,
                LastUpdated = DateTime.UtcNow
            };

            var response = await client.PostAsJsonAsync(inventoryUrl, inventoryPayload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to sync product to InventoryService. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while syncing new product to InventoryService.");
        }

        return CreatedAtAction(
            nameof(GetProduct),
            new { id = product.ProductId },
            product);
    }

    // PUT: /catalog/v1/products/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(Guid id, Product product)
    {
        if (id != product.ProductId)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Product ID",
                detail: "The product ID in the URL does not match the product ID in the request body."
            );
        }

        if (string.IsNullOrWhiteSpace(product.Name))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Product",
                detail: "Product name is required."
            );
        }

        if (product.Price < 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Product",
                detail: "Product price cannot be negative."
            );
        }

        if (product.StockQuantity < 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Product",
                detail: "Stock quantity cannot be negative."
            );
        }

        var existingProduct = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (existingProduct == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: $"Product with ID {id} was not found."
            );
        }

        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.Price = product.Price;
        existingProduct.StockQuantity = product.StockQuantity;
        existingProduct.IsActive = product.IsActive;
        existingProduct.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: /catalog/v1/products/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product Not Found",
                detail: $"Product with ID {id} was not found."
            );
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}