using Autofac;
using CryptoSuite.Config;
using CryptoSuite.KeyManagement.Factories;
using CryptoSuite.KeyManagement.Interfaces;
using CryptoSuite.Services;
using CryptoSuite.Services.Interfaces;

namespace KeyManagerTool.Services.Modules
{
    public class CryptoSuiteModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // 初始化靜態 CryptoConfig 設定
            CryptoConfig.Load("appsettings.json");

            // 註冊工廠
            builder.RegisterType<KeyGeneratorFactory>().As<IKeyGeneratorFactory>().SingleInstance();
            builder.RegisterType<KeyLoaderFactory>().As<IKeyLoaderFactory>().SingleInstance();

            // 註冊服務
            builder.RegisterType<CryptoKeyService>().As<ICryptoKeyService>().SingleInstance();
            builder.RegisterType<CryptoService>().As<ICryptoService>().SingleInstance();
        }
    }
}