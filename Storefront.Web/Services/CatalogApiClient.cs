using System.Net;
using System.Net.Http.Json;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

public class CatalogApiClient
{
    private readonly HttpClient _httpClient;

    public CatalogApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _httpClient.GetFromJsonAsync<List<ProductDto>>(
            "catalog/v1/products", cancellationToken);

        return products ?? new List<ProductDto>();
    }

    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"catalog/v1/products/{productId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
    }

    public async Task<(ProductDto? product, string? error)> CreateProductAsync(ProductDto product, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("catalog/v1/products", product, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (null, await ReadErrorAsync(response, cancellationToken));
        }

        var created = await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
        return (created, null);
    }

    public async Task<(bool ok, string? error)> UpdateProductAsync(Guid productId, ProductDto product, CancellationToken cancellationToken = default)
    {
        // The Catalog API requires the body's ProductId to match the route id.
        product.ProductId = productId;

        var response = await _httpClient.PutAsJsonAsync($"catalog/v1/products/{productId}", product, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (false, await ReadErrorAsync(response, cancellationToken));
        }

        return (true, null);
    }

    public async Task<(bool ok, string? error)> DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"catalog/v1/products/{productId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return (false, "That product no longer exists.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return (false, await ReadErrorAsync(response, cancellationToken));
        }

        return (true, null);
    }

    // The Catalog API returns ProblemDetails (title/detail) on validation and not-found errors.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(cancellationToken);
            var message = problem?.Detail ?? problem?.Title;
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }
        catch
        {
            // fall through to a generic message
        }

        return $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
    }

    private sealed record ProblemResponse(string? Title, string? Detail);
}
