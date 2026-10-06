using Microsoft.EntityFrameworkCore;
using InventoryService.Api.Data;
using InventoryService.Api.Services;
using InventoryService.Api.Workers;
using Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context Setup (InventoryDb)
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("InventoryConnection")));

// 2. Controllers & Swagger Configuration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

// 3. Correlation ID Propagation Setup
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdHandler>();

// 4. RabbitMQ Publisher at Saga Consumer Registration
builder.Services.AddTransient<RabbitMqPublisher>();
builder.Services.AddHostedService<InventorySagaConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();