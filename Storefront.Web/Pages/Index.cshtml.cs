using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;
using System.Net.Http.Json;

namespace Storefront.Web.Pages;

public class IndexModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly CartService _cart;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        CatalogApiClient catalog,
        CartService cart,
        IHttpClientFactory httpClientFactory,
        ILogger<IndexModel> logger)
    {
        _catalog = catalog;
        _cart = cart;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public List<ProductDto> Products { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public string? CatalogError { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadProductsAsync();
    }

    public async Task<IActionResult> OnPostAddToCartAsync(Guid productId, int quantity)
    {
        if (quantity < 1)
        {
            quantity = 1;
        }

        var product = await _catalog.GetProductAsync(productId);

        if (product == null)
        {
            StatusMessage = "error|That product is no longer available.";
            return RedirectToPage();
        }

        _cart.Add(product, quantity);
        StatusMessage = $"success|Added {quantity} × {product.Name} to your cart.";

        return RedirectToPage(new { Search });
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            Products = await _catalog.GetProductsAsync();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                Products = Products
                    .Where(p => p.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
                             || p.Description.Contains(Search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // --- FETCH LIVE STOCK FROM INVENTORY SERVICE (PORT 7015) ---
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
                            // Direct Quantity para hindi mag-double deduct dahil sa ReservedQuantity
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