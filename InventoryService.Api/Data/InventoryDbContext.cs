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
        modelBuilder.Entity<InventoryItem>().HasData(
            new InventoryItem
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ProductId = Guid.Parse("a1b2c3d4-e5f6-7890-1234-56789abcdef0"),
                Quantity = 100,
                ReservedQuantity = 0,
                LastUpdated = DateTime.UtcNow
            },
            new InventoryItem
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ProductId = Guid.Parse("b2c3d4e5-f6a7-8901-2345-6789abcdef01"),
                Quantity = 50,
                ReservedQuantity = 0,
                LastUpdated = DateTime.UtcNow
            }
        );
    }
}