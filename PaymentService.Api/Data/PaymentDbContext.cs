using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace PaymentService.Api.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map column details to strictly align with SQL schema
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(p => p.Status).HasMaxLength(50);
            entity.Property(p => p.PaymentMethod).HasMaxLength(50).HasDefaultValue("CreditCard");
            entity.Property(p => p.FailureReason).HasMaxLength(255);
        });
    }
}