using Microsoft.EntityFrameworkCore;
using OrderService.Api.Consumers;
using OrderService.Api.Data;
using OrderService.Api.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context Setup
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("OrderConnection")));

// 2. Controllers & Swagger Configuration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// 3. HttpContextAccessor & Correlation ID Handler Setup
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<Shared.Logging.CorrelationIdHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// 4. Messaging Services
builder.Services.AddScoped<RabbitMqPublisher>();
builder.Services.AddHostedService<OrderSagaConsumer>();

// 5. Generated Typed HttpClient Registration w/ Resilience Policy & Correlation ID Handler
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(client =>
{
    client.BaseAddress = new Uri("https://localhost:7124/");
})
.AddHttpMessageHandler<Shared.Logging.CorrelationIdHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.UseJitter = true;

    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();