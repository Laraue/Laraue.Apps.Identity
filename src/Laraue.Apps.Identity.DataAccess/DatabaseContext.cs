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

            // TelegramId is supplied by the caller (it's Telegram's own account id), not generated
            // by the database - without this, EF defaults a `long` PK to an identity column.
            builder.Property(x => x.TelegramId).ValueGeneratedNever();

            builder
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserService>(builder =>
        {
            builder.HasKey(x => new { x.UserId, x.ServiceId });

            builder
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);

            builder
                .HasOne(x => x.Service)
                .WithMany()
                .HasForeignKey(x => x.ServiceId);
        });
    }
}
