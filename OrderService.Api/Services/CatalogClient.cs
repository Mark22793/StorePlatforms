using System.Net.Http.Json;

namespace OrderService.Api.Services;

public class CatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogProduct?> GetProductAsync(int productId)
    {
        return await _httpClient.GetFromJsonAsync<CatalogProduct>(
            $"catalog/v1/products/{productId}");
    }
}

public class CatalogProduct
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
}