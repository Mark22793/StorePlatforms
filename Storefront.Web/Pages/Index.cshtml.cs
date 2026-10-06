using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;

namespace Storefront.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<ProductDto> Products { get; set; } = new();

    [BindProperty]
    public Guid SelectedProductId { get; set; }

    [BindProperty]
    public string CustomerName { get; set; } = string.Empty;

    [BindProperty]
    public int Quantity { get; set; } = 1;

    public async Task OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CatalogService");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = await client.GetFromJsonAsync<List<ProductDto>>("/catalog/v1/products", options);
            Products = response ?? new List<ProductDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Catalog Fetch Error: {ex.Message}");
            Products = new List<ProductDto>();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        try
        {
            var payload = new CreateOrderRequest
            {
                CustomerName = CustomerName,
                Items = new List<CreateOrderItemRequest>
                {
                    new CreateOrderItemRequest
                    {
                        ProductId = SelectedProductId,
                        Quantity = Quantity
                    }
                }
            };

            var client = _httpClientFactory.CreateClient("OrderService");

            var response = await client.PostAsJsonAsync("/orders/v1/orders", payload);

            if (response.IsSuccessStatusCode)
            {
                var createdOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
                if (createdOrder != null)
                {
                    return RedirectToPage("/OrderStatus", new { id = createdOrder.OrderId });
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] OrderService Failed with Status {response.StatusCode}: {errorContent}");
                ModelState.AddModelError(string.Empty, $"Order API Error ({(int)response.StatusCode} {response.StatusCode}): {errorContent}");
                await OnGetAsync();
                return Page();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Connection Exception: {ex.Message}");
            ModelState.AddModelError(string.Empty, $"Connection Exception: {ex.Message}");
            await OnGetAsync();
            return Page();
        }

        await OnGetAsync();
        return Page();
    }
}