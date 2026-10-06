using System.Text.Json.Serialization;

namespace Storefront.Web.Models;

public class ProductDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("productId")]
    public Guid ProductId { get; set; }

    [JsonIgnore]
    public Guid ValidProductId => ProductId != Guid.Empty ? ProductId : Id;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("stockQuantity")]
    public int StockQuantity { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}
