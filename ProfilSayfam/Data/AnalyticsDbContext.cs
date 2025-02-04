using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ProfilSayfam.Models.Analytics;
using ProfilSayfam.Models;

namespace ProfilSayfam.Data
{
    public class AnalyticsDbContext : DbContext
    {
        public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options)
        {
        }

        public DbSet<PageView> PageViews { get; set; }
        public DbSet<Conversion> Conversions { get; set; }
        public DbSet<Models.Analytics.Visit> Visits { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PageView>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PageName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ViewedAt).IsRequired();
                entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.UserAgent).IsRequired().HasMaxLength(500);
            });

            modelBuilder.Entity<Conversion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ConvertedAt).IsRequired();
                entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.UserAgent).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ConversionType).IsRequired().HasMaxLength(50).HasDefaultValue("General");
                entity.Property(e => e.FormData).IsRequired(false);
            });

            modelBuilder.Entity<Models.Analytics.Visit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.UserAgent).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Path).IsRequired().HasMaxLength(255);
                entity.Property(e => e.VisitedAt).IsRequired();
                entity.Property(e => e.Country).HasMaxLength(100);
                entity.Property(e => e.City).HasMaxLength(100);
                entity.Property(e => e.Region).HasMaxLength(100);
                entity.Property(e => e.Isp).HasMaxLength(200);
                entity.Property(e => e.Latitude).HasColumnType("decimal(10, 8)");
                entity.Property(e => e.Longitude).HasColumnType("decimal(11, 8)");
            });
        }
    }

    public class AnalyticsDbContextFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
    {
        public AnalyticsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AnalyticsDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=77.245.159.23;Database=ergunozb_;User Id=ErgunDb;Password=Spivovic7;TrustServerCertificate=True;"
            );

            return new AnalyticsDbContext(optionsBuilder.Options);
        }
    }
} 