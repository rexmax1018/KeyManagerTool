# 資料庫 (.mdf) 還原指南

本文件說明如何使用 Entity Framework Core Migrations 來管理和還原專案中的資料庫 (.mdf) 檔案。

## 資料庫檔案路徑

本專案的資料庫檔案 `KeyManagerDb.mdf` 預期會存在於以下路徑（根據 `KeyManagerTool/appsettings.json` 中的連線字串設定）：

`E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf`

## 還原資料庫 (.mdf 檔案) 的步驟

如果您需要重新建立或更新上述路徑的資料庫檔案，請依照以下步驟操作：

1.  **開啟 Visual Studio 中的開發人員 PowerShell**
    在 Visual Studio 中，依序點擊 `工具 (Tools)` -> `命令列 (Command Line)` -> `開發人員 PowerShell (Developer PowerShell)`。

2.  **導航到解決方案根目錄**
    在「開發人員 PowerShell」視窗中，導航到您的解決方案根目錄 (通常是包含 `.sln` 檔案的資料夾)。

3.  **確保 `dotnet-ef` 工具已安裝**
    如果您尚未安裝 EF Core 命令列工具，請先安裝：
    ```powershell
    dotnet tool install --global dotnet-ef --version 7.* # 確保版本與您的 EF Core 套件匹配 (e.g., 7.x)
    ```
    如果已安裝但版本不正確，可以先解除安裝再安裝：
    ```powershell
    dotnet tool uninstall --global dotnet-ef
    dotnet tool install --global dotnet-ef --version 7.*
    ```
    安裝或更新後，請務必 **重新啟動您的 PowerShell 視窗**。

4.  **新增遷移 (Add-Migration)**
    如果您對 `KeyManagerTool.Dao/Models/Customer.cs` 模型或其他資料庫結構進行了更改，您需要新增一個新的遷移來記錄這些更改：
    ```powershell
    dotnet ef migrations add YourMigrationName --project KeyManagerTool.Dao --startup-project KeyManagerTool
    ```
    例如，您可以將 `YourMigrationName` 替換為描述本次更改的名稱，如 `AddCustomerAddress`。這將會產生一個新的遷移檔案在 `Migrations` 資料夾中。

5.  **更新資料庫 (Update-Database)**
    這是實際建立或更新 `.mdf` 檔案的關鍵步驟。執行此指令後，Entity Framework Core 會檢查您的遷移記錄，並將資料庫更新到最新的狀態。
    ```powershell
    dotnet ef database update --project KeyManagerTool.Dao --startup-project KeyManagerTool
    ```
    執行此指令後：
    * 如果 `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf` 檔案不存在，Entity Framework Core 會嘗試在此路徑建立一個新的資料庫檔案。
    * 如果檔案存在，它會應用所有尚未執行的遷移，使資料庫結構與您的程式碼模型保持一致。

## 常見問題與錯誤排除 (EF Core)

### 錯誤：`Unable to create an object of type 'AppDbContext'.`

這個錯誤表示 EF Core 設計時工具無法建立您的 `AppDbContext` 實例。這通常是因為 `DbContext` 的建構子需要參數（例如 `DbContextOptions<AppDbContext>`），而設計時工具不知道如何提供。

**解決方案**：
在 `KeyManagerTool.Dao` 專案中實作 `IDesignTimeDbContextFactory<AppDbContext>` 介面。您需要建立一個類別 (例如 `AppDbContextFactory.cs`)，內容類似於以下範例：

```csharp
// KeyManagerTool.Dao/AppDbContextFactory.cs (範例)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace KeyManagerTool.Dao
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}