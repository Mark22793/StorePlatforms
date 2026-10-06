using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;

namespace Storefront.Web.Pages;

public class OrderStatusModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OrderStatusModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public OrderDto? Order { get; set; }

    public async Task OnGetAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("OrderService");

        var response = await client.GetAsync(
            $"/orders/v1/orders/{id}"
        );

        if (!response.IsSuccessStatusCode)
        {
            Order = null;
            return;
        }

        Order = await response.Content
            .ReadFromJsonAsync<OrderDto>();
    }
}