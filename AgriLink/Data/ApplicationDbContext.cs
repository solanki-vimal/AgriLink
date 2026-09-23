using AgriLink.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AgriLink.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Produce> ProduceListings { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<MarketPrice> MarketPrices { get; set; }
        public DbSet<PriceHistory> PriceHistories { get; set; }
        public DbSet<FarmerProfile> FarmerProfiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // required - sets up Identity tables

            // User (Farmer) -> Produce : 1-to-many
            modelBuilder.Entity<Produce>()
                .HasOne(p => p.Farmer)
                .WithMany(u => u.ProduceListings)
                .HasForeignKey(p => p.FarmerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Category -> Produce : 1-to-many
            modelBuilder.Entity<Produce>()
                .HasOne(p => p.Category)
                .WithMany(c => c.ProduceListings)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Produce -> Order : 1-to-many
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Produce)
                .WithMany(p => p.Orders)
                .HasForeignKey(o => o.ProduceId)
                .OnDelete(DeleteBehavior.Restrict);

            // User (Buyer) -> Order : 1-to-many
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Buyer)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Category -> MarketPrice : 1-to-many
            modelBuilder.Entity<MarketPrice>()
                .HasOne(mp => mp.Category)
                .WithMany(c => c.MarketPrices)
                .HasForeignKey(mp => mp.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // User (Admin) -> MarketPrice : 1-to-many
            modelBuilder.Entity<MarketPrice>()
                .HasOne(mp => mp.SetByAdmin)
                .WithMany(u => u.MarketPricesSet)
                .HasForeignKey(mp => mp.SetByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            // Produce -> PriceHistory : 1-to-many
            modelBuilder.Entity<PriceHistory>()
                .HasOne(ph => ph.Produce)
                .WithMany(p => p.PriceHistories)
                .HasForeignKey(ph => ph.ProduceId)
                .OnDelete(DeleteBehavior.Cascade);

            // User (Farmer) -> FarmerProfile : 1-to-1 (shared primary key)
            modelBuilder.Entity<FarmerProfile>()
                .HasOne(fp => fp.User)
                .WithOne(u => u.FarmerProfile)
                .HasForeignKey<FarmerProfile>(fp => fp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Seed data - Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { CategoryId = 1, Name = "Vegetables", Description = "Fresh vegetables" },
                new Category { CategoryId = 2, Name = "Fruits", Description = "Seasonal fruits" },
                new Category { CategoryId = 3, Name = "Grains & Pulses", Description = "Cereals, pulses and grains" },
                new Category { CategoryId = 4, Name = "Spices", Description = "Spices and condiments" }
            );  
        }
    }
}
