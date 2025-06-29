using Autofac;
using KeyManagerTool;
using KeyManagerTool.Modules;
using NLog;
using CryptoSuite.Config;

Console.WriteLine("[KeyManagerTool] 正在啟動...");

// --- NLog 配置開始 ---
LogManager.Setup().LoadConfigurationFromFile("NLog.config");
// --- NLog 配置結束 ---

// 取得 NLog Logger 實例用於 Program.cs 的日誌
var mainLogger = LogManager.GetCurrentClassLogger();

// 建立 Autofac Container Builder
var builder = new ContainerBuilder();

// 1. 先註冊 CryptoSuiteModule，它會初始化 CryptoConfig
builder.RegisterModule<CryptoSuiteModule>();

// 2. 為了在註冊 KeyManagementModule 時獲取 basePath，我們需要一個臨時容器
using (var tempContainer = builder.Build())
{
    var keyManagerBasePath = Path.Combine(Directory.GetCurrentDirectory(), CryptoConfig.Current.KeyDirectory);

    // 重置 builder，準備最終容器的註冊
    builder = new ContainerBuilder();
    builder.RegisterModule<CryptoSuiteModule>(); // 再次註冊 CryptoSuiteModule 到最終 builder
    builder.RegisterModule(new AppServicesModule()); // 註冊應用程式通用服務模組

    // 註冊 KeyManagementModule，並傳入 basePath
    builder.RegisterModule(new KeyManagementModule(keyManagerBasePath));
}

// 構建最終的容器
using var container = builder.Build();

try
{
    // 解析並執行金鑰產生器
    var generator = container.Resolve<KeyGenerator>(); // KeyGenerator 現在會自動接收 ILogger 和 basePath
    generator.Generate();

    // 解析 KeyManagerService
    var keyService = container.Resolve<KeyManagerService>(); // KeyManagerService 現在會自動接收 ILogger 和 basePath
    await keyService.StartAsync();

    Console.WriteLine("[KeyManagerTool] 程式執行完畢。");
}
catch (Exception ex)
{
    // 捕獲任何未被內部處理的嚴重錯誤
    mainLogger.Fatal(ex, "應用程式發生未預期的嚴重錯誤，請檢查日誌。");
    Console.WriteLine("應用程式因錯誤終止，詳情請參閱日誌。");
}
finally
{
    // 確保 NLog 在程式結束前關閉，將所有緩衝的日誌寫入
    LogManager.Shutdown();
}