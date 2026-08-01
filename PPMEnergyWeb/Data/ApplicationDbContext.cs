using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PPMEnergyWeb.Models;

namespace PPMEnergyWeb.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductAttribute> ProductAttributes { get; set; }
        public DbSet<HomeConfig> HomeConfigs { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<QuoteDetail> QuoteDetails { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierCategory> SupplierCategories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<QuoteDetail>()
                .Property(q => q.Price)
                .HasPrecision(18, 2);

            // ✅ เพิ่ม: บอก EF ว่า Category มี self-reference ผ่าน ParentCategoryId
            // WithMany() ไม่ใส่ navigation เพราะ SubCategories เป็น [NotMapped]
            builder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany()
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired(false);

            // ✅ ความสัมพันธ์ Supplier ↔ Category (many-to-many ผ่าน SupplierCategory)
            builder.Entity<SupplierCategory>()
                .HasOne(sc => sc.Supplier)
                .WithMany(s => s.SupplierCategories)
                .HasForeignKey(sc => sc.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SupplierCategory>()
                .HasOne(sc => sc.Category)
                .WithMany()
                .HasForeignKey(sc => sc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}