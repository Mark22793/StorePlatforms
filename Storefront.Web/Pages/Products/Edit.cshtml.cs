using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;
using System.Net.Http.Json;

namespace Storefront.Web.Pages.Products;

public class EditModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        CatalogApiClient catalog,
        IHttpClientFactory httpClientFactory,
        ILogger<EditModel> logger)
    {
        _catalog = catalog;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty]
    public CustomProductInputModel Input { get; set; } = new();

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var product = await _catalog.GetProductAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        Input = new CustomProductInputModel
        {
            ProductId = product.ValidProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity
        };

        // Kuhanin ang live stock mula sa Inventory Service (Port 7015)
        try
        {
            var client = _httpClientFactory.CreateClient();
            var inventoryItems = await client.GetFromJsonAsync<List<EditInventoryDto>>("https://localhost:7015/inventory/v1/Inventories");
            var inv = inventoryItems?.FirstOrDefault(i => i.ProductId == id);
            if (inv != null)
            {
                Input.StockQuantity = Math.Max(0, inv.Quantity - inv.ReservedQuantity);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch inventory stock for Edit.");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // Direct object initialization gamit ang id parameter
            var productDto = new ProductDto
            {
                Name = Input.Name,
                Description = Input.Description,
                Price = Input.Price,
                StockQuantity = Input.StockQuantity
            };

            // 1. Update Catalog Service details
            await _catalog.UpdateProductAsync(id, productDto);

            // 2. Update Inventory Microservice stock directly (Port 7015)
            try
            {
                var client = _httpClientFactory.CreateClient();

                var inventoryItems = await client.GetFromJsonAsync<List<EditInventoryDto>>("https://localhost:7015/inventory/v1/Inventories");
                var inv = inventoryItems?.FirstOrDefault(i => i.ProductId == id);

                if (inv != null)
                {
                    var updatePayload = new
                    {
                        id = inv.Id,
                        productId = id,
                        quantity = Input.StockQuantity + inv.ReservedQuantity,
                        reservedQuantity = inv.ReservedQuantity
                    };

                    await client.PutAsJsonAsync($"https://localhost:7015/inventory/v1/Inventories/{inv.Id}", updatePayload);
                }
            }
            catch (Exception invEx)
            {
                _logger.LogError(invEx, "Failed to update Inventory Service stock.");
            }

            TempData["StatusMessage"] = $"success|Product '{Input.Name}' updated successfully!";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product");
            Error = "Failed to update product. " + ex.Message;
            return Page();
        }
    }

    public class CustomProductInputModel
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }
}

public record EditInventoryDto(Guid Id, Guid ProductId, int Quantity, int ReservedQuantity);