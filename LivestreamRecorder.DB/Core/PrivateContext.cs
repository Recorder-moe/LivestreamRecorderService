using LivestreamRecorder.DB.Models;
using Microsoft.EntityFrameworkCore;
#nullable disable warnings

namespace LivestreamRecorder.DB.Core;

public class PrivateContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public PrivateContext() { }

    public PrivateContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        #region Users
        modelBuilder.Entity<User>()
            .ToContainer("Users");

        modelBuilder.Entity<User>()
            .HasNoDiscriminator();

        modelBuilder.Entity<User>()
            .HasKey(nameof(User.id));

        modelBuilder.Entity<User>()
            .HasPartitionKey(o => o.id);

        modelBuilder.Entity<User>()
            .UseETagConcurrency();
        #endregion

        #region Transactions
        modelBuilder.Entity<Transaction>()
            .ToContainer("Transactions");

        modelBuilder.Entity<Transaction>()
            .HasNoDiscriminator();

        modelBuilder.Entity<Transaction>()
            .HasKey(nameof(Transaction.id));

        modelBuilder.Entity<Transaction>()
            .HasPartitionKey(o => o.UserId);

        modelBuilder.Entity<Transaction>()
            .UseETagConcurrency();

        modelBuilder.Entity<Transaction>()
            .HasOne(o => o.User)
            .WithMany(o => o.Transactions)
            .HasForeignKey(o => o.UserId);

        #endregion

        #region Other Examples
        //#region PropertyNames
        //modelBuilder.Entity<Video>().OwnsOne(
        //    o => o.ShippingAddress,
        //    sa =>
        //    {
        //        sa.ToJsonProperty("Address");
        //        sa.Property(p => p.Street).ToJsonProperty("ShipsToStreet");
        //        sa.Property(p => p.City).ToJsonProperty("ShipsToCity");
        //    });
        //#endregion

        //#region OwnsMany
        //modelBuilder.Entity<Distributor>().OwnsMany(p => p.ShippingCenters);
        //#endregion

        //#region ETagProperty
        //modelBuilder.Entity<Distributor>()
        //    .Property(d => d.ETag)
        //    .IsETagConcurrency();
        //#endregion
        #endregion
    }
}