using Microsoft.EntityFrameworkCore;
using KeyManagerTool.Dao.Models;

namespace KeyManagerTool.Dao
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // 在 EF Core 中，Database.SetInitializer<TContext>(null) 不再適用。
            // 初始化的邏輯通常透過 DbContextOptions 來配置，或者在應用程式啟動時使用 Migrate()。
        }

        public DbSet<Customer> Customers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Customer>().ToTable("Customers");
        }
    }
}