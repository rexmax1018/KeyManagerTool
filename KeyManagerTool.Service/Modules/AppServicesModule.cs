using Autofac;
using KeyManagerTool.Dao;
using KeyManagerTool.Dao.Interfaces;
using KeyManagerTool.Dao.Repositories;
using KeyManagerTool.Service.Interfaces;
using KeyManagerTool.Service.Services;
using Microsoft.Extensions.Configuration;
using NLog;

namespace KeyManagerTool.Service.Modules
{
    public class AppServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // 註冊 AppDbContext
            builder.Register(c =>
            {
                var configuration = c.Resolve<IConfiguration>(); // 從容器中獲取 IConfiguration
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                return new AppDbContext(connectionString);
            }).InstancePerLifetimeScope(); // 確保每個生命週期範圍內有一個 DbContext 實例

            // 註冊 DAO 層的儲存庫
            builder.RegisterType<CustomerRepository>().As<ICustomerRepository>().InstancePerLifetimeScope();

            // 註冊應用程式服務
            builder.RegisterType<CustomerService>().As<ICustomerService>().InstancePerLifetimeScope();
        }
    }
}