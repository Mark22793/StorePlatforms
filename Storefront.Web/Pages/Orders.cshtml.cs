using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages;

public class OrdersModel : PageModel
{
    private readonly OrderApiClient _orders;
    private readonly ILogger<OrdersModel> _logger;

    public OrdersModel(OrderApiClient orders, ILogger<OrdersModel> logger)
    {
        _orders = orders;
        _logger = logger;
    }

    public List<OrderDto> Orders { get; set; } = new();

    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Orders = (await _orders.GetOrdersAsync())
                .OrderByDescending(o => o.CreatedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load orders");
            Error = ex.Message;
        }
    }
}
