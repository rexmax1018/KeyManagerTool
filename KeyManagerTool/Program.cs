using Autofac;
using KeyManagerTool;
using KeyManagerTool.Modules;

Console.WriteLine("[KeyManagerTool] 正在啟動...");

// 建立 Autofac Container
var builder = new ContainerBuilder();
builder.RegisterModule<CryptoSuiteModule>(); // 註冊 CryptoConfig 與加解密服務
builder.RegisterType<KeyGenerator>().AsSelf().SingleInstance(); // 註冊測試金鑰產生器

using var container = builder.Build();

// 解析並執行金鑰產生器
var generator = container.Resolve<KeyGenerator>();
generator.Generate(); //

var keyService = new KeyManagerService();
// 呼叫 StartAsync，其內部包含了 FileSystemWatcher 的初始化和首次掃描
// 由於 FileSystemWatcher 是非阻塞的，程式會在 StartAsync 執行完畢後繼續
await keyService.StartAsync(); //

// 程式將在 StartAsync() 啟動 FileSystemWatcher 後，自然結束。
// 因為沒有其他程式碼會阻塞主執行緒。
Console.WriteLine("[KeyManagerTool] 程式執行完畢。");