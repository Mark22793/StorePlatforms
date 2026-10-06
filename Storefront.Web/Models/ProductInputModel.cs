using System.ComponentModel.DataAnnotations;

namespace Storefront.Web.Models;

// Form model for creating/editing a product (with validation for the UI).
public class ProductInputModel
{
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Range(0, 9_999_999, ErrorMessage = "Price must be 0 or greater.")]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative.")]
    [Display(Name = "Stock quantity")]
    public int StockQuantity { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public ProductDto ToDto() => new()
    {
        ProductId = ProductId,
        Name = Name,
        Description = Description ?? string.Empty,
        Price = Price,
        StockQuantity = StockQuantity,
        IsActive = IsActive
    };

    public static ProductInputModel FromDto(ProductDto dto) => new()
    {
        ProductId = dto.ValidProductId,
        Name = dto.Name,
        Description = dto.Description,
        Price = dto.Price,
        StockQuantity = dto.StockQuantity,
        IsActive = dto.IsActive
    };
}
