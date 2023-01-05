using LivestreamRecorderService.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace LivestreamRecorderService.DB.Core;

public class PublicContext : DbContext
{
    public DbSet<Video> Videos { get; set; }

    public PublicContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        #region Videos
        #region Container
        modelBuilder.Entity<Video>()
            .ToContainer("Videos");
        #endregion

        #region NoDiscriminator
        modelBuilder.Entity<Video>()
            .HasNoDiscriminator();
        #endregion

        modelBuilder.Entity<Video>()
            .HasKey(nameof(Video.id));

        #region PartitionKey
        modelBuilder.Entity<Video>()
            .HasPartitionKey(o => o.ChannelId);
        #endregion

        #region ETag
        modelBuilder.Entity<Video>()
            .UseETagConcurrency();
        #endregion
        #endregion

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
    }
}