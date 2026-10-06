using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Products;

public class CreateModel : PageModel
{
    private readonly CatalogApiClient _catalog;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(CatalogApiClient catalog, ILogger<CreateModel> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    [BindProperty]
    public ProductInputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var (created, error) = await _catalog.CreateProductAsync(Input.ToDto());

            if (created == null)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create the product.");
                return Page();
            }

            StatusMessage = $"success|Product “{Input.Name}” created.";
            return RedirectToPage("/Products/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product");
            ModelState.AddModelError(string.Empty,
                $"Can't reach the Catalog Service (https://localhost:7124). {ex.Message}");
            return Page();
        }
    }
}
