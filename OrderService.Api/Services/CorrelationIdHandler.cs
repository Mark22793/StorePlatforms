namespace OrderService.Api.Services;

public class CorrelationIdHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();

        request.Headers.TryAddWithoutValidation(
            "X-Correlation-ID",
            correlationId);

        return await base.SendAsync(request, cancellationToken);
    }
}
