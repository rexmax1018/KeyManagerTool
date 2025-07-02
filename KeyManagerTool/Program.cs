using Autofac;
using KeyManagerTool.CryptoLib.Interfaces;
using KeyManagerTool.CryptoLib.Modules;
using KeyManagerTool.CryptoLib.Services;
using KeyManagerTool.Domain;
using KeyManagerTool.Service.Interfaces;
using KeyManagerTool.Service.Modules;
using Microsoft.Extensions.Configuration;
using NLog;

Console.WriteLine("[KeyManagerTool] 正在啟動...");

// --- NLog 配置開始 ---
LogManager.Setup().LoadConfigurationFromFile("NLog.config");
var mainLogger = LogManager.GetCurrentClassLogger();
// --- NLog 配置結束 ---

// --- 配置載入開始 ---
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();
// --- 配置載入結束 ---

var builder = new ContainerBuilder();

// 註冊 IConfiguration 實例
builder.RegisterInstance(configuration).As<IConfiguration>().SingleInstance();
// 註冊主 Logger 實例
builder.RegisterInstance(mainLogger).As<ILogger>().SingleInstance();

// 載入應用程式服務模組 (包含 CustomerService)
builder.RegisterModule(new AppServicesModule());

// 從 appsettings.json 獲取金鑰基礎路徑
var keyManagerBasePath = Path.Combine(Directory.GetCurrentDirectory(), configuration.GetValue<string>("CryptoSuite:KeyDirectory"));

// 載入 KeyManagerTool.CryptoLib 中的模組
builder.RegisterModule(new CryptoSuiteModule()); // 註冊 CryptoSuite 的核心服務
builder.RegisterModule(new CoreServicesModule(keyManagerBasePath, mainLogger)); // 註冊 KeyManagerService 和 DataEncryptionService

using var container = builder.Build();

try
{
    using (var scope = container.BeginLifetimeScope())
    {
        var customerService = scope.Resolve<ICustomerService>();
        var keyManagerService = scope.Resolve<KeyManagerService>();
        var dataEncryptionService = scope.Resolve<IDataEncryptionService>();

        // 1. 不再在這裡生成金鑰組。金鑰生成由獨立的 KeyManagerTool.KeyGeneratorApp 完成。
        //    您需要單獨執行 KeyManagerTool.KeyGeneratorApp 來生成金鑰。

        // 2. 觸發 KeyManagerService 處理金鑰搬移 (從 update 到 current/history)
        //    這一步會將外部生成的金鑰移到 current。
        mainLogger.Info("開始檢查並處理 update 資料夾中的新金鑰組...");

        await keyManagerService.StartAsync();

        mainLogger.Info("金鑰處理程序完成。");

        // 3. 獲取當前活動金鑰的 unifiedName (用於新增數據和重新加密舊數據)
        string currentActiveUnifiedName = keyManagerService.GetLatestActiveUnifiedName();
        if (string.IsNullOrEmpty(currentActiveUnifiedName))
        {
            mainLogger.Error("無法獲取當前活動的 unifiedName。請確保金鑰已由 KeyGeneratorApp 生成並成功處理。應用程式將終止。");
            return;
        }
        mainLogger.Info($"當前活動金鑰的 unifiedName: {currentActiveUnifiedName}");

        mainLogger.Info("--- 開始客戶資料操作 ---");

        // 4. 新增一筆新的客戶資料 (確保唯一性)
        string uniqueName = $"Test Customer_{DateTime.Now:yyyyMMddHHmmssfff}";
        string uniqueEmail = $"test.customer.{DateTime.Now.Ticks}@example.com";

        var newCustomer = CustomerDomain.CreateNew(
            0, // ID 由資料庫自動生成，設為 0
            uniqueName,
            uniqueEmail,
            dataEncryptionService, // 傳入 IDataEncryptionService
            currentActiveUnifiedName // 傳入當前活動的金鑰名稱
        );
        await customerService.AddCustomerAsync(newCustomer);
        mainLogger.Info($"新增客戶成功，Name: {newCustomer.Name}, Email (encrypted preview): {newCustomer.GetEncryptedEmailDataForPersistence().Substring(0, Math.Min(newCustomer.GetEncryptedEmailDataForPersistence().Length, 30))}...");
        mainLogger.Info($"   解密後 Email: {newCustomer.Email}");

        // 5. 重新加密所有舊資料庫中的數據 (資料遷移模擬)
        mainLogger.Info("--- 開始重新加密舊資料庫中的客戶 Email ---");
        var allCustomers = await customerService.GetAllCustomersAsync();
        int reEncryptedCount = 0;

        foreach (var customer in allCustomers)
        {
            try
            {
                string oldEncryptedEmailData = customer.GetEncryptedEmailDataForPersistence();
                string oldUnifiedName = dataEncryptionService.GetUnifiedNameFromEncryptedData(oldEncryptedEmailData);

                // 如果舊資料的 unifiedName 與當前活動的 unifiedName 不同，則重新加密
                if (oldUnifiedName != currentActiveUnifiedName)
                {
                    mainLogger.Info($"客戶 '{customer.Name}' 的 Email (ID: {customer.Id}) 正在從舊金鑰 '{oldUnifiedName}' 重新加密至新金鑰 '{currentActiveUnifiedName}'。");

                    // 1. 解密舊資料 (Email 屬性的 Getter 會自動解密)
                    string decryptedEmail = customer.Email;

                    // 2. 使用最新的金鑰重新加密
                    string newEncryptedData = dataEncryptionService.Encrypt(decryptedEmail, currentActiveUnifiedName);

                    // 3. 更新 Domain 實體的加密數據
                    customer.UpdateEncryptedEmailDataForMigration(newEncryptedData);

                    // 4. 更新資料庫
                    await customerService.UpdateCustomerAsync(customer);
                    reEncryptedCount++;
                    mainLogger.Info($"   客戶 '{customer.Name}' 的 Email 已成功重新加密。");
                }
                else
                {
                    mainLogger.Debug($"客戶 '{customer.Name}' 的 Email (ID: {customer.Id}) 已是最新金鑰 '{oldUnifiedName}'，無需重新加密。");
                }
            }
            catch (Exception ex)
            {
                mainLogger.Error(ex, $"重新加密客戶 '{customer.Name}' (ID: {customer.Id}) 的 Email 時發生錯誤。跳過此筆。");
            }
        }
        mainLogger.Info($"--- 重新加密舊資料完成，共 {reEncryptedCount} 筆資料被重新加密 ---");

        // 6. 再次讀取所有客戶，確認資料狀態
        mainLogger.Info("--- 再次讀取所有客戶，確認狀態 ---");
        var finalCustomers = await customerService.GetAllCustomersAsync();
        foreach (var customer in finalCustomers)
        {
            mainLogger.Info($"客戶 ID: {customer.Id}, Name: {customer.Name}, Email (decrypted): {customer.Email}, UnifiedName: {dataEncryptionService.GetUnifiedNameFromEncryptedData(customer.GetEncryptedEmailDataForPersistence())}");
        }
        mainLogger.Info("--- 客戶資料操作完成 ---");
    }
}
catch (Exception ex)
{
    mainLogger.Fatal(ex, "應用程式發生未預期的嚴重錯誤，請檢查日誌。");
    Console.WriteLine("應用程式因錯誤終止，詳情請參閱日誌。");
}
finally
{
    LogManager.Shutdown();
}