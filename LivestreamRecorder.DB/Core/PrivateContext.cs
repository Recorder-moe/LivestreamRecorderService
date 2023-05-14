using LivestreamRecorder.DB.Models;
using Microsoft.EntityFrameworkCore;
#nullable disable warnings

namespace LivestreamRecorder.DB.Core;

public class PrivateContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public PrivateContext() { }

    public PrivateContext(DbContextOptions<PrivateContext> options) : base(options)
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
    }
}