using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Products;

public class EditModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly ILogger<EditModel> _logger;

    public EditModel(CatalogApiClient catalog, ILogger<EditModel> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    [BindProperty]
    public ProductInputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        try
        {
            var product = await _catalog.GetProductAsync(id);
            if (product == null)
            {
                StatusMessage = "error|That product no longer exists.";
                return RedirectToPage("/Products/Index");
            }

            Input = ProductInputModel.FromDto(product);
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
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var (ok, error) = await _catalog.UpdateProductAsync(Input.ProductId, Input.ToDto());

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update the product.");
                return Page();
            }

            StatusMessage = $"success|Product “{Input.Name}” updated.";
            return RedirectToPage("/Products/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product {Id}", Input.ProductId);
            ModelState.AddModelError(string.Empty,
                $"Can't reach the Catalog Service (https://localhost:7124). {ex.Message}");
            return Page();
        }
    }
}
