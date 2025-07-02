using Autofac;
using KeyManagerTool.CryptoLib.Modules;
using KeyManagerTool.KeyGeneratorApp;
using Microsoft.Extensions.Configuration;
using NLog;

Console.WriteLine("[KeyGeneratorApp] 正在啟動金鑰生成器...");

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
builder.RegisterInstance(mainLogger).As<ILogger>().SingleInstance(); // 註冊 Logger

// 載入 CryptoSuiteModule 來註冊 CryptoSuite 相關服務
builder.RegisterModule(new CryptoSuiteModule());

// 從 appsettings.json 獲取金鑰基礎路徑
var keyManagerBasePath = Path.Combine(Directory.GetCurrentDirectory(), configuration.GetValue<string>("CryptoSuite:KeyDirectory"));

// 註冊 KeyGenerator
builder.RegisterType<KeyGenerator>()
       .AsSelf()
       .WithParameter("basePath", keyManagerBasePath)
       .SingleInstance();

using var container = builder.Build();

try
{
    using (var scope = container.BeginLifetimeScope())
    {
        var keyGenerator = scope.Resolve<KeyGenerator>();
        keyGenerator.Generate();
        mainLogger.Info("金鑰生成程序完成。");
    }
}
catch (Exception ex)
{
    mainLogger.Fatal(ex, "金鑰生成應用程式發生未預期的嚴重錯誤。");
    Console.WriteLine("金鑰生成應用程式因錯誤終止，詳情請參閱日誌。");
}
finally
{
    LogManager.Shutdown();
}