using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages;

public class IndexModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly CartService _cart;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(CatalogApiClient catalog, CartService cart, ILogger<IndexModel> logger)
    {
        _catalog = catalog;
        _cart = cart;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load products from Catalog Service");
            CatalogError = ex.Message;
        }
    }
}
