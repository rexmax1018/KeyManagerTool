using Autofac;
using KeyManagerTool;
using KeyManagerTool.Modules;

Console.WriteLine("[KeyManagerTool] 正在啟動...");

// 建立 Autofac Container
var builder = new ContainerBuilder();
builder.RegisterModule<CryptoSuiteModule>(); // 註冊 CryptoConfig 與加解密服務
builder.RegisterType<TestKeyGenerator>().AsSelf().SingleInstance(); // 註冊測試金鑰產生器

using var container = builder.Build();

// 解析並執行金鑰產生器
var generator = container.Resolve<TestKeyGenerator>();
generator.Generate();

var keyService = new KeyManagerService();
await keyService.StartAsync();