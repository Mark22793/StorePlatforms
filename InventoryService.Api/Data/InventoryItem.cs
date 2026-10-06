namespace InventoryService.Api.Data;

public class InventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 0;
    public int ReservedQuantity { get; set; } = 0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}