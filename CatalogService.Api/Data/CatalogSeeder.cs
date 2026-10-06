using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CatalogService.Api.Data;

public static class CatalogSeeder
{
    /// <summary>
    /// Creates the Catalog database if it doesn't exist and seeds sample products when empty.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogDbContext>>();

        await EnsureDatabaseAsync(context, logger);

        if (await context.Products.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        context.Products.AddRange(
            // Fixed IDs so InventoryService can seed matching stock rows (see InventoryDbContext)
            NewProduct("a1b2c3d4-e5f6-7890-1234-56789abcdef0", "Wireless Mouse", "Ergonomic 2.4GHz wireless mouse with silent clicks.", 799.00m, 50, now),
            NewProduct("b2c3d4e5-f6a7-8901-2345-6789abcdef01", "Mechanical Keyboard", "Hot-swappable keyboard with brown switches and RGB backlight.", 3499.00m, 25, now),
            NewProduct("c3d4e5f6-a7b8-9012-3456-789abcdef012", "USB-C Hub", "7-in-1 hub with HDMI, SD card reader and 100W pass-through charging.", 1599.00m, 40, now),
            NewProduct("d4e5f6a7-b8c9-0123-4567-89abcdef0123", "27\" Monitor", "27-inch IPS 1440p monitor, 75Hz, with thin bezels.", 12999.00m, 8, now),
            NewProduct("e5f6a7b8-c9d0-1234-5678-9abcdef01234", "Laptop Stand", "Adjustable aluminum stand for 11–17 inch laptops.", 1199.00m, 30, now),
            NewProduct("f6a7b8c9-d0e1-2345-6789-abcdef012345", "Noise-Cancelling Headphones", "Over-ear Bluetooth headphones with 30-hour battery.", 5499.00m, 5, now));

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded Catalog database with sample products");
    }

    /// <summary>
    /// Creates the database and tables only when they are really missing.
    /// EnsureCreated decides "missing" from a failed login to the database itself, which on
    /// LocalDB can happen for an existing database and then fails with "file already exists".
    /// Asking master instead gives a reliable answer.
    /// </summary>
    private static async Task EnsureDatabaseAsync(CatalogDbContext context, ILogger logger)
    {
        var builder = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        bool exists;
        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = new SqlCommand("SELECT DB_ID(@name)", master);
            command.Parameters.AddWithValue("@name", databaseName);
            exists = await command.ExecuteScalarAsync() is not (null or DBNull);
        }

        var creator = context.GetService<IRelationalDatabaseCreator>();

        if (!exists)
        {
            logger.LogInformation("Creating database {Database}", databaseName);
            await creator.CreateAsync();
        }

        if (!await creator.HasTablesAsync())
        {
            logger.LogInformation("Creating tables in {Database}", databaseName);
            await creator.CreateTablesAsync();
        }
    }

    private static Product NewProduct(string id, string name, string description, decimal price, int stock, DateTime now) => new()
    {
        ProductId = Guid.Parse(id),
        Name = name,
        Description = description,
        Price = price,
        StockQuantity = stock,
        IsActive = true,
        CreatedAt = now,
        UpdatedAt = now
    };
}
