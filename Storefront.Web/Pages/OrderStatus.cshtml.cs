using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages;

public class OrderStatusModel : PageModel
{
    private readonly OrderApiClient _orders;
    private readonly CatalogApiClient _catalog;
    private readonly ILogger<OrderStatusModel> _logger;

    public OrderStatusModel(OrderApiClient orders, CatalogApiClient catalog, ILogger<OrderStatusModel> logger)
    {
        _orders = orders;
        _catalog = catalog;
        _logger = logger;
    }

    public OrderDto? Order { get; set; }

    public string? Error { get; set; }

    /// <summary>Product names looked up from the Catalog Service, keyed by product ID.</summary>
    public Dictionary<Guid, string> ProductNames { get; set; } = new();

    public bool IsPending => Order != null && string.Equals(Order.Status, "Pending", StringComparison.OrdinalIgnoreCase);

    public async Task OnGetAsync(Guid id)
    {
        try
        {
            Order = await _orders.GetOrderAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load order {OrderId}", id);
            Error = $"Can't reach the Order Service. {ex.Message}";
            return;
        }

        if (Order == null)
        {
            return;
        }

        foreach (var productId in Order.Items.Select(i => i.ProductId).Distinct())
        {
            try
            {
                var product = await _catalog.GetProductAsync(productId);
                if (product != null)
                {
                    ProductNames[productId] = product.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not look up product {ProductId}", productId);
            }
        }
    }

    public string NameFor(Guid productId) =>
        ProductNames.TryGetValue(productId, out var name) ? name : productId.ToString()[..8];
}
