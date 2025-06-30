using Autofac;
using NLog;

namespace KeyManagerTool.Services.Modules
{
    public class AppServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // 註冊 NLog Logger 實例
            builder.RegisterInstance(LogManager.GetCurrentClassLogger()).As<ILogger>().SingleInstance();
        }
    }
}