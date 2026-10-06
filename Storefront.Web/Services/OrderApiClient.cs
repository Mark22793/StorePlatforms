using System.Text.Json;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

public class OrderApiClient
{
    private readonly HttpClient _httpClient;

    public OrderApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<OrderDto>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _httpClient.GetFromJsonAsync<List<OrderDto>>(
            "orders/v1/orders", cancellationToken);

        return orders ?? new List<OrderDto>();
    }

    public async Task<OrderDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"orders/v1/orders/{orderId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OrderDto>(cancellationToken);
    }

    /// <summary>
    /// Places an order. Returns the created order, or an error message taken from the API's ProblemDetails.
    /// </summary>
    public async Task<(OrderDto? Order, string? Error)> CreateOrderAsync(
        CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("orders/v1/orders", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var order = await response.Content.ReadFromJsonAsync<OrderDto>(cancellationToken);
            return order == null
                ? (null, "Order Service returned an empty response.")
                : (order, null);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (null, ReadProblemDetail(body) ?? $"Order Service returned {(int)response.StatusCode} {response.StatusCode}.");
    }

    private static string? ReadProblemDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;

            if (title != null && detail != null)
            {
                return $"{title}: {detail}";
            }

            return detail ?? title;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
