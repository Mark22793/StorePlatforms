using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages.Products;

public class DeleteModel : PageModel
{
    private readonly CatalogApiClient _catalogApiClient;

    public DeleteModel(CatalogApiClient catalogApiClient)
    {
        _catalogApiClient = catalogApiClient;
    }

    [BindProperty]
    public ProductDto Product { get; set; } = new();

    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var products = await _catalogApiClient.GetProductsAsync();
        var product = products.FirstOrDefault(p => p.ValidProductId == id);

        if (product == null)
        {
            return RedirectToPage("Index");
        }

        Product = product;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        try
        {
            await _catalogApiClient.DeleteProductAsync(id);
            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            Error = ex.Message;

            var products = await _catalogApiClient.GetProductsAsync();
            var product = products.FirstOrDefault(p => p.ValidProductId == id);
            if (product != null)
            {
                Product = product;
            }

            return Page();
        }
    }
}