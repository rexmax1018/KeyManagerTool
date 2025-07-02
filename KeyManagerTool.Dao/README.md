# 資料庫 (.mdf) 還原指南

本文件說明如何使用 Entity Framework Code First Migrations 來管理和還原專案中的資料庫 (.mdf) 檔案。

## 資料庫檔案路徑

本專案的資料庫檔案 `KeyManagerDb.mdf` 預期會存在於以下路徑（根據 `appsettings.json` 和 `KeyManagerTool.Dao/App.config` 中的連線字串設定）：

`E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf`

## 還原資料庫 (.mdf 檔案) 的步驟

如果您需要重新建立或更新上述路徑的資料庫檔案，請依照以下步驟操作：

1.  **開啟 Visual Studio 中的套件管理員主控台 (Package Manager Console)**
    在 Visual Studio 中，依序點擊 `工具 (Tools)` -> `NuGet 套件管理員 (NuGet Package Manager)` -> `套件管理員主控台 (Package Manager Console)`。

2.  **設定預設專案 (Set Default Project)**
    在「套件管理員主控台」視窗中，確認 **預設專案 (Default project)** 下拉選單已設定為 `KeyManagerTool.Dao`。這是因為資料庫遷移相關的指令需要在包含 `DbContext` 和 Migrations 檔案的專案中執行。

3.  **啟用遷移 (Enable Migrations) (如果尚未啟用)**
    如果這是您第一次設定遷移，或者不確定是否已啟用，請執行以下指令：
    ```powershell
    Enable-Migrations
    ```
    *注意：本專案已包含遷移檔案，此步驟通常會被跳過。*

4.  **新增遷移 (Add-Migration) (如果資料庫模型有變更)**
    如果您對 `KeyManagerTool.Dao/Models/Customer.cs` 模型或其他資料庫結構進行了更改，您需要新增一個新的遷移來記錄這些更改：
    ```powershell
    Add-Migration YourMigrationName
    ```
    例如，您可以將 `YourMigrationName` 替換為描述本次更改的名稱，如 `AddCustomerAddress`。這將會產生一個新的遷移檔案在 `Migrations` 資料夾中。

5.  **更新資料庫 (Update-Database)**
    這是實際建立或更新 `.mdf` 檔案的關鍵步驟。執行此指令後，Entity Framework 會檢查您的遷移記錄，並將資料庫更新到最新的狀態。
    ```powershell
    Update-Database
    ```
    執行此指令後：
    * 如果 `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf` 檔案不存在，Entity Framework 會嘗試在此路徑建立一個新的資料庫檔案。
    * 如果檔案存在，它會應用所有尚未執行的遷移，使資料庫結構與您的程式碼模型保持一致。

## 常見問題與錯誤排除

### 錯誤：`Cannot attach the file '...' as database '...'`

這個錯誤通常表示 SQL Server (LocalDB) 無法將指定的 `.mdf` 檔案附加為資料庫。這可能是由於檔案被佔用、權限不足或 LocalDB 執行個體問題。

請依照以下順序嘗試這些解決方案：

1.  **確保沒有其他程式佔用檔案：**
    * **重新啟動 Visual Studio：** 這是最常見且有效的解決方案。關閉所有 Visual Studio 實例，然後重新啟動。
    * **檢查 SQL Server Management Studio (SSMS) 或 Azure Data Studio：** 如果您有開啟這些工具並連接到該資料庫，請斷開連接並關閉這些工具。
    * **檢查工作管理員：** 確保沒有 `sqlservr.exe` 或 `SqlLocalDB.exe` 的相關進程正在運行，或者直接重新啟動電腦。
    * **移除隱藏的 `~` 臨時檔案：** 有時會有一些隱藏的臨時檔案（例如 `~KeyManagerDb.mdf`）鎖定資料庫，確保這些檔案也被刪除。

2.  **檢查檔案權限：**
    * 導航到 `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\` 資料夾。
    * 右鍵點擊 `KeyManagerDb.mdf` (如果存在) 和 `KeyManagerDb_log.ldf` (如果存在) 檔案，選擇 `內容 (Properties)`。
    * 進入 `安全性 (Security)` 選項卡。
    * 確保 `USERS`、`SYSTEM` 或您的帳戶具有 **完全控制 (Full Control)** 的權限。如果沒有，請點擊 `編輯 (Edit)` 並添加或修改權限。

3.  **徹底刪除現有資料庫檔案並重新建立：**
    如果上述方法無效，最直接的方法是讓 Entity Framework 重新建立全新的資料庫檔案。
    * **關閉 Visual Studio。**
    * 導航到 `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\` 資料夾。
    * **刪除** `KeyManagerDb.mdf` 和 `KeyManagerDb_log.ldf` 這兩個檔案 (如果它們