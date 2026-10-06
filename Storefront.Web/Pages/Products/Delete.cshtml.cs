using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Products;

public class DeleteModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly ILogger<DeleteModel> _logger;

    public DeleteModel(CatalogApiClient catalog, ILogger<DeleteModel> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public ProductDto? Product { get; set; }

    [BindProperty]
    public Guid Id { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            Product = await _catalog.GetProductAsync(id);
            if (Product == null)
            {
                StatusMessage = "error|That product no longer exists.";
                return RedirectToPage("/Products/Index");
            }

            Id = id;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load product {Id}", id);
            StatusMessage = $"error|Can't reach the Catalog Service. {ex.Message}";
            return RedirectToPage("/Products/Index");
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var (ok, error) = await _catalog.DeleteProductAsync(Id);
            StatusMessage = ok ? "success|Product deleted." : $"error|{error}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete product {Id}", Id);
            StatusMessage = $"error|Can't reach the Catalog Service. {ex.Message}";
        }

        return RedirectToPage("/Products/Index");
    }
}
