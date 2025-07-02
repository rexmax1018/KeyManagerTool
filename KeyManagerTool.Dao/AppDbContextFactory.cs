using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KeyManagerTool.Dao
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // 1. 設定 ConfigurationBuilder 以讀取 appsettings.json
            // 這會模擬主應用程式如何載入配置
            // 注意：需要確保路徑指向啟動專案的 appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // 設置基礎路徑為當前執行目錄
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 2. 從配置中取得連接字串
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // 3. 建立 DbContextOptionsBuilder 並設定資料庫供應商
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            // 4. 返回 AppDbContext 的實例
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}