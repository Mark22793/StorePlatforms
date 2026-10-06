using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;

namespace Storefront.Web.Pages;

public class CartModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CartModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<OrderItemDto> CartItems { get; set; } = new();

    // Temporary cart data for testing
    private void LoadCartItems()
    {
        CartItems = new List<OrderItemDto>
        {
            new OrderItemDto
            {
                ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Quantity = 2,
                UnitPrice = 100.00m
            },

            new OrderItemDto
            {
                ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Quantity = 1,
                UnitPrice = 250.00m
            }
        };
    }

    public void OnGet()
    {
        LoadCartItems();
    }

    public async Task<IActionResult> OnPostCheckoutAsync(string customerName)
    {
        // Load cart items again because POST is a new request
        LoadCartItems();

        if (string.IsNullOrWhiteSpace(customerName))
        {
            ModelState.AddModelError(
                string.Empty,
                "Customer name is required."
            );

            return Page();
        }

        if (!CartItems.Any())
        {
            ModelState.AddModelError(
                string.Empty,
                "Your cart is empty."
            );

            return Page();
        }

        var client = _httpClientFactory.CreateClient("OrderService");

        var createOrderPayload = new
        {
            CustomerName = customerName,

            Items = CartItems.Select(item => new
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList()
        };

        var response = await client.PostAsJsonAsync(
            "/orders/v1/orders",
            createOrderPayload
        );

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(
                string.Empty,
                "Failed to create order. Please try again."
            );

            return Page();
        }

        var createdOrder =
            await response.Content.ReadFromJsonAsync<OrderDto>();

        if (createdOrder == null)
        {
            return RedirectToPage("/Error");
        }

        return RedirectToPage(
            "/OrderStatus",
            new { id = createdOrder.OrderId }
        );
    }
}