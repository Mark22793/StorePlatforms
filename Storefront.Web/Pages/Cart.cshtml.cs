using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Storefront.Web.Pages;

public class CartModel : PageModel
{
    private readonly CartService _cart;
    private readonly OrderApiClient _orders;
    private readonly ILogger<CartModel> _logger;

    public CartModel(CartService cart, OrderApiClient orders, ILogger<CartModel> logger)
    {
        _cart = cart;
        _orders = orders;
        _logger = logger;
    }

    public List<CartItem> CartItems { get; set; } = new();

    public decimal Total => CartItems.Sum(i => i.Subtotal);

    [BindProperty]
    public string CustomerName { get; set; } = string.Empty;

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
        CartItems = _cart.GetItems();
    }

    public IActionResult OnPostUpdate(Guid productId, int quantity)
    {
        _cart.UpdateQuantity(productId, quantity);
        return RedirectToPage();
    }

    public IActionResult OnPostRemove(Guid productId)
    {
        _cart.Remove(productId);
        StatusMessage = "success|Item removed from cart.";
        return RedirectToPage();
    }

    public IActionResult OnPostClear()
    {
        _cart.Clear();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCheckoutAsync()
    {
        CartItems = _cart.GetItems();

        if (!CartItems.Any())
        {
            ModelState.AddModelError(string.Empty, "Your cart is empty.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ModelState.AddModelError(nameof(CustomerName), "Customer name is required.");
            return Page();
        }

        var request = new CreateOrderRequest
        {
            CustomerName = CustomerName.Trim(),
            Items = CartItems.Select(i => new CreateOrderItemRequest
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        try
        {
            var (order, error) = await _orders.CreateOrderAsync(request);

            if (order == null)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create order.");
                return Page();
            }

            _cart.Clear();
            return RedirectToPage("/OrderStatus", new { id = order.OrderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach Order Service");
            ModelState.AddModelError(string.Empty,
                $"Can't reach the Order Service (https://localhost:7054). {ex.Message}");
            return Page();
        }
    }
}
