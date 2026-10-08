using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;
using System.Net.Http.Json;

namespace Storefront.Web.Pages.Products;

public class IndexModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        CatalogApiClient catalog,
        IHttpClientFactory httpClientFactory,
        ILogger<IndexModel> logger)
    {
        _catalog = catalog;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<ProductDto> Products { get; set; } = new();

    public string? CatalogError { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            // 1. Get products from Catalog Service
            Products = await _catalog.GetProductsAsync() ?? new();

            // 2. FETCH LIVE STOCK FROM INVENTORY SERVICE (PORT 7015)
            try
            {
                var client = _httpClientFactory.CreateClient();
                var inventoryItems = await client.GetFromJsonAsync<List<InventoryItemDto>>("https://localhost:7015/inventory/v1/Inventories");

                if (inventoryItems != null && inventoryItems.Any())
                {
                    foreach (var product in Products)
                    {
                        var inv = inventoryItems.FirstOrDefault(i => i.ProductId == product.ValidProductId);
                        if (inv != null)
                        {
                            // Direct Quantity para patas ang bilang sa Shop page
                            product.StockQuantity = Math.Max(0, inv.Quantity);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not sync live stock from InventoryService. Falling back to Catalog stock.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load products from Catalog Service");
            CatalogError = ex.Message;
        }
    }
}

public record InventoryItemDto(Guid Id, Guid ProductId, int Quantity, int ReservedQuantity);