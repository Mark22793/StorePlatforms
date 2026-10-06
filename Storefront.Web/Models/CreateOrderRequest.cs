namespace Storefront.Web.Models;

public class CreateOrderRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    public Guid ProductId { get; set; } // Siguraduhing Guid ito (hindi int)
    public int Quantity { get; set; }
}