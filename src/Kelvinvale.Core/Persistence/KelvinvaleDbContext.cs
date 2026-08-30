using Kelvinvale.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kelvinvale.Core.Persistence;

public class KelvinvaleDbContext(DbContextOptions<KelvinvaleDbContext> options) : DbContext(options)
{
    public DbSet<Adviser> Advisers => Set<Adviser>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Holding> Holdings => Set<Holding>();

    public DbSet<Instruction> Instructions => Set<Instruction>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no native DateTimeOffset type, so it cannot filter or sort on one. Store it as
        // a sortable binary value; the offset is preserved.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var taxYear = new ValueConverter<TaxYear, int>(
            v => v.StartYear,
            v => TaxYear.FromStartYear(v));

        var nullableTaxYear = new ValueConverter<TaxYear?, int?>(
            v => v.HasValue ? v.Value.StartYear : null,
            v => v.HasValue ? TaxYear.FromStartYear(v.Value) : null);

        modelBuilder.Entity<Adviser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.HasMany(x => x.Customers).WithOne(x => x.Adviser).HasForeignKey(x => x.AdviserId);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).IsRequired();
            e.Property(x => x.LastName).IsRequired();
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.Address).IsRequired();
            e.HasIndex(x => x.AdviserId);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.TaxYear).HasConversion(nullableTaxYear);
            e.HasOne(x => x.Customer).WithMany(x => x.Products).HasForeignKey(x => x.CustomerId);
            e.HasMany(x => x.Holdings).WithOne(x => x.Product).HasForeignKey(x => x.ProductId);
            e.HasMany(x => x.Instructions).WithOne(x => x.Product).HasForeignKey(x => x.ProductId);

            // "A customer may hold one ISA per tax year" - enforced at the database, not just in a service.
            e.HasIndex(x => new { x.CustomerId, x.TaxYear })
                .IsUnique()
                .HasFilter($"\"Type\" = {(int)ProductType.Isa}");
        });

        modelBuilder.Entity<Holding>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FundCode).IsRequired();
        });

        modelBuilder.Entity<Instruction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.FundCode).IsRequired();
            e.Property(x => x.ClientReference).IsRequired();
            e.Property(x => x.TaxYear).HasConversion(taxYear);

            // Client-supplied idempotency key, unique per product.
            e.HasIndex(x => new { x.ProductId, x.ClientReference }).IsUnique();
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).IsRequired();
            e.Property(x => x.SubjectType).IsRequired();
            e.Property(x => x.Outcome).HasConversion<int>();
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.OccurredAtUtc);
        });
    }
}
