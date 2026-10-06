var builder = WebApplication.CreateBuilder(args);

// 1. Add Razor Pages
builder.Services.AddRazorPages();

// 2. Register HTTP Clients para sa backend services
builder.Services.AddHttpClient("CatalogService", client =>
{
    // Tiyaking nakaturo sa port 7124 (CatalogService)
    client.BaseAddress = new Uri("https://localhost:7124/");
});

builder.Services.AddHttpClient("OrderService", client =>
{
    // Inayos mula 7189 patungong 7054 batay sa launchSettings.json
    client.BaseAddress = new Uri("https://localhost:7054/");
});

var app = builder.Build();

// 3. Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();