using Laraue.Apps.Identity.DataAccess.Data;
using Laraue.Apps.Identity.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Laraue.Apps.Identity.DataAccess;

public class DatabaseContext : DbContext
{
    public DatabaseContext(DbContextOptions options)
        : base(options)
    {
    }

    public required DbSet<User> Users { get; set; }
    public required DbSet<Service> Services { get; set; }
    public required DbSet<TelegramAccount> TelegramAccounts { get; set; }
    public required DbSet<UserService> UserServices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Service>()
            .HasData(ServicesData.Services);

        modelBuilder.Entity<TelegramAccount>(builder =>
        {
            builder.HasKey(x => x.TelegramId);
            builder.Property(x => x.TelegramId).ValueGeneratedNever();
        });

        modelBuilder.Entity<UserService>(builder =>
        {
            builder.HasKey(x => new { x.UserId, x.ServiceId });
        });
    }
}
