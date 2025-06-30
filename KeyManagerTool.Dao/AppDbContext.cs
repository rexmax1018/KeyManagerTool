// KeyManagerTool.Dao/AppDbContext.cs (最終修正後)
using System.Data.Entity;
using KeyManagerTool.Dao.Models;
using System.Configuration;
using KeyManagerTool.Dao.Migrations;

namespace KeyManagerTool.Dao
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(string connectionString) : base(connectionString)
        {
            Database.SetInitializer<AppDbContext>(null);
        }

        public AppDbContext() : base("name=DefaultConnection")
        {
            Database.SetInitializer<AppDbContext>(null);
        }

        public DbSet<Customer> Customers { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}