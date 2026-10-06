using System.Net.Http.Json;

namespace OrderService.Api.Services;

public interface ICatalogClient
{
    Task<CatalogItemDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default);
}

public class CatalogClient : ICatalogClient
{
    private readonly HttpClient _httpClient;

    public CatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CatalogItemDto?> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/catalog/v1/products/{productId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CatalogItemDto>(cancellationToken: cancellationToken);
    }
}

public record CatalogItemDto(
    Guid ProductId,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity);