using Storefront.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Razor Pages + session (session holds the shopping cart)
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CartService>();

// 2. Typed HTTP clients for the backend services (URLs from appsettings.json -> ServiceUrls)
var catalogUrl = builder.Configuration["ServiceUrls:Catalog"] ?? "https://localhost:7124/";
var orderUrl = builder.Configuration["ServiceUrls:Order"] ?? "https://localhost:7054/";

builder.Services.AddHttpClient<CatalogApiClient>(client =>
{
    client.BaseAddress = new Uri(catalogUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<OrderApiClient>(client =>
{
    client.BaseAddress = new Uri(orderUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
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
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
