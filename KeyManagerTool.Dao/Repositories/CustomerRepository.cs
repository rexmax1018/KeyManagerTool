using KeyManagerTool.Dao.Interfaces;
using KeyManagerTool.Dao.Models;
using NLog;
using Microsoft.EntityFrameworkCore;

namespace KeyManagerTool.Dao.Repositories
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger _logger;

        public CustomerRepository(AppDbContext dbContext, ILogger logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            _logger.Debug("從資料庫獲取所有客戶。");

            return await _dbContext.Customers.ToListAsync();
        }

        public async Task<Customer> GetCustomerByIdAsync(int id)
        {
            _logger.Debug($"從資料庫獲取 ID 為 {id} 的客戶。");

            return await _dbContext.Customers.FindAsync(id);
        }

        public async Task AddCustomerAsync(Customer customer)
        {
            _logger.Debug($"新增客戶: {customer.Name}");

            _dbContext.Customers.Add(customer);

            await Task.CompletedTask;
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            _logger.Debug($"更新客戶: {customer.Name} (ID: {customer.Id})");

            _dbContext.Customers.Update(customer);

            await Task.CompletedTask;
        }

        public async Task DeleteCustomerAsync(int id)
        {
            _logger.Debug($"刪除 ID 為 {id} 的客戶。");

            var customer = await _dbContext.Customers.FindAsync(id);

            if (customer != null)
            {
                _dbContext.Customers.Remove(customer);
            }
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            _logger.Debug("提交資料庫變更。");

            await _dbContext.SaveChangesAsync();
        }
    }
}