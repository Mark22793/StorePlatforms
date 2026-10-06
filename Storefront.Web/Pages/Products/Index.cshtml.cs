using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Products;

public class IndexModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(CatalogApiClient catalog, ILogger<IndexModel> logger)
    {
        _catalog = catalog;
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
            Products = await _catalog.GetProductsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load products from Catalog Service");
            CatalogError = ex.Message;
        }
    }
}
