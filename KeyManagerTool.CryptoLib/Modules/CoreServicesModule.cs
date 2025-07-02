using Autofac;
using KeyManagerTool.CryptoLib.Interfaces;
using KeyManagerTool.CryptoLib.Services;
using NLog;

namespace KeyManagerTool.CryptoLib.Modules
{
    public class CoreServicesModule : Module
    {
        private readonly string _keyManagerBasePath;
        private readonly ILogger _logger;

        public CoreServicesModule(string keyManagerBasePath, ILogger logger)
        {
            _keyManagerBasePath = keyManagerBasePath;
            _logger = logger;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // 註冊 KeyManagerService
            builder.RegisterType<KeyManagerService>()
                   .AsSelf()
                   .WithParameter("logger", _logger)
                   .WithParameter("basePath", _keyManagerBasePath)
                   .SingleInstance();

            // 註冊 IDataEncryptionService 及其實作
            builder.RegisterType<DataEncryptionService>()
                   .As<IDataEncryptionService>()
                   .SingleInstance();
        }
    }
}