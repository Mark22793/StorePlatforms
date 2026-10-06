namespace Storefront.Web.Models;

public class CartItem
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public decimal Subtotal => UnitPrice * Quantity;
}
