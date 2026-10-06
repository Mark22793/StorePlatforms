using Microsoft.EntityFrameworkCore;

namespace InventoryService.Api.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<InventoryItem> Inventories => Set<InventoryItem>();
    public DbSet<InventoryItem> InventoryItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seeding Data na kapareho ng nasa SQL script
        // ProductIds match the products seeded by CatalogService (CatalogSeeder)
        var seededAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<InventoryItem>().HasData(
            Stock("11111111-1111-1111-1111-111111111111", "a1b2c3d4-e5f6-7890-1234-56789abcdef0", 50, seededAt),  // Wireless Mouse
            Stock("22222222-2222-2222-2222-222222222222", "b2c3d4e5-f6a7-8901-2345-6789abcdef01", 25, seededAt),  // Mechanical Keyboard
            Stock("33333333-3333-3333-3333-333333333333", "c3d4e5f6-a7b8-9012-3456-789abcdef012", 40, seededAt),  // USB-C Hub
            Stock("44444444-4444-4444-4444-444444444444", "d4e5f6a7-b8c9-0123-4567-89abcdef0123", 8, seededAt),   // 27" Monitor
            Stock("55555555-5555-5555-5555-555555555555", "e5f6a7b8-c9d0-1234-5678-9abcdef01234", 30, seededAt),  // Laptop Stand
            Stock("66666666-6666-6666-6666-666666666666", "f6a7b8c9-d0e1-2345-6789-abcdef012345", 5, seededAt)    // Noise-Cancelling Headphones
        );
    }

    private static InventoryItem Stock(string id, string productId, int quantity, DateTime seededAt) => new()
    {
        Id = Guid.Parse(id),
        ProductId = Guid.Parse(productId),
        Quantity = quantity,
        ReservedQuantity = 0,
        LastUpdated = seededAt
    };
}