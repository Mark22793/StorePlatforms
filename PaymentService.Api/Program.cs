using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Data;
using PaymentService.Api.Services;
using PaymentService.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PaymentConnection")
    ));

// RabbitMQ Publisher
builder.Services.AddScoped<RabbitMqPublisher>();

// Payment Saga Consumer
builder.Services.AddHostedService<PaymentSagaConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();