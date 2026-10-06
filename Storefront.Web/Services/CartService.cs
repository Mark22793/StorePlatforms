using System.Text.Json;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

/// <summary>
/// Shopping cart stored in the user's session.
/// </summary>
public class CartService
{
    private const string SessionKey = "Cart";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CartService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ISession Session => _httpContextAccessor.HttpContext!.Session;

    public List<CartItem> GetItems()
    {
        var json = Session.GetString(SessionKey);
        return string.IsNullOrEmpty(json)
            ? new List<CartItem>()
            : JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>();
    }

    public int Count => GetItems().Sum(i => i.Quantity);

    public void Add(ProductDto product, int quantity)
    {
        var items = GetItems();
        var existing = items.FirstOrDefault(i => i.ProductId == product.ValidProductId);

        if (existing == null)
        {
            items.Add(new CartItem
            {
                ProductId = product.ValidProductId,
                Name = product.Name,
                UnitPrice = product.Price,
                Quantity = quantity
            });
        }
        else
        {
            existing.Quantity += quantity;
            existing.UnitPrice = product.Price;
        }

        Save(items);
    }

    public void UpdateQuantity(Guid productId, int quantity)
    {
        var items = GetItems();
        var existing = items.FirstOrDefault(i => i.ProductId == productId);

        if (existing == null)
        {
            return;
        }

        if (quantity <= 0)
        {
            items.Remove(existing);
        }
        else
        {
            existing.Quantity = quantity;
        }

        Save(items);
    }

    public void Remove(Guid productId) => UpdateQuantity(productId, 0);

    public void Clear() => Session.Remove(SessionKey);

    private void Save(List<CartItem> items) =>
        Session.SetString(SessionKey, JsonSerializer.Serialize(items));
}
