using Autofac;
using KeyManagerTool;
using KeyManagerTool.Modules; // 確保此命名空間存在
using NLog;
using CryptoSuite.Config;

Console.WriteLine("[KeyManagerTool] 正在啟動...");

// --- NLog 配置開始 ---
LogManager.Setup().LoadConfigurationFromFile("NLog.config");
// --- NLog 配置結束 ---

// 建立 Autofac Container Builder
var builder = new ContainerBuilder();

// 1. 先註冊 CryptoSuiteModule，它會初始化 CryptoConfig
builder.RegisterModule<CryptoSuiteModule>();

// 2. 為了在註冊 KeyManagementModule 時獲取 basePath，我們需要一個臨時容器
//    這是一種處理需要在模組註冊前獲取配置的模式
using (var tempContainer = builder.Build())
{
    // 從 CryptoConfig 中取得金鑰根目錄路徑
    // CryptoConfig.Current 在 CryptoSuiteModule 載入後才可用
    var keyManagerBasePath = Path.Combine(Directory.GetCurrentDirectory(), CryptoConfig.Current.KeyDirectory);

    // 3. 現在註冊 AppServicesModule (通用服務，如 NLog)
    builder = new ContainerBuilder(); // 重置 builder，準備最終容器的註冊
    builder.RegisterModule<CryptoSuiteModule>(); // 再次註冊 CryptoSuiteModule 到最終 builder
    builder.RegisterModule(new AppServicesModule()); // 註冊應用程式通用服務模組

    // 4. 註冊 KeyManagementModule，並傳入 basePath
    builder.RegisterModule(new KeyManagementModule(keyManagerBasePath));
}

// 構建最終的容器
using var container = builder.Build();

// 解析並執行金鑰產生器
var generator = container.Resolve<KeyGenerator>();
generator.Generate();

// 解析 KeyManagerService
var keyService = container.Resolve<KeyManagerService>();
await keyService.StartAsync();

Console.WriteLine("[KeyManagerTool] 程式執行完畢。");

// 在程式結束前，關閉 NLog，確保所有日誌都已寫入
LogManager.Shutdown();