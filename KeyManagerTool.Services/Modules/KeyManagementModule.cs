using Autofac;

namespace KeyManagerTool.Services.Modules
{
    public class KeyManagementModule : Module
    {
        private readonly string _basePath;

        // 建構函式接受 basePath 參數
        public KeyManagementModule(string basePath)
        {
            _basePath = basePath;
        }

        protected override void Load(ContainerBuilder builder)
        {
            // 註冊 KeyGenerator，並將 basePath 作為參數傳遞
            builder.RegisterType<KeyGenerator>()
                   .AsSelf()
                   .WithParameter(new NamedParameter("basePath", _basePath))
                   .SingleInstance();

            // 註冊 KeyManagerService，並將 basePath 作為參數傳遞
            builder.RegisterType<KeyManagerService>()
                   .AsSelf()
                   .WithParameter(new NamedParameter("basePath", _basePath))
                   .SingleInstance();
        }
    }
}