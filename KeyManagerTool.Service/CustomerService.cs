using KeyManagerTool.Dao.Interfaces;
using KeyManagerTool.Domain;
using KeyManagerTool.Domain.Interfaces;
using KeyManagerTool.Service.Interfaces;
using NLog;

namespace KeyManagerTool.Service.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IDataEncryptionService _dataEncryptionService;
        private readonly ILogger _logger;

        public CustomerService(
            ICustomerRepository customerRepository,
            IDataEncryptionService dataEncryptionService,
            ILogger logger)
        {
            _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
            _dataEncryptionService = dataEncryptionService ?? throw new ArgumentNullException(nameof(dataEncryptionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<CustomerDomain>> GetAllCustomersAsync()
        {
            _logger.Info("獲取所有客戶 Domain 實體。");
            var daoCustomers = await _customerRepository.GetAllCustomersAsync();
            var domainCustomers = new List<CustomerDomain>();

            foreach (var daoCustomer in daoCustomers)
            {
                var domainCustomer = new CustomerDomain(
                    daoCustomer.Id,
                    daoCustomer.Name,
                    daoCustomer.Email,
                    _dataEncryptionService
                );
                domainCustomer.SetEncryptedEmailDataFromPersistence(daoCustomer.Email);
                domainCustomers.Add(domainCustomer);
            }
            return domainCustomers;
        }

        public async Task<CustomerDomain> GetCustomerByIdAsync(int id)
        {
            _logger.Info($"獲取 ID 為 {id} 的客戶 Domain 實體。");
            var daoCustomer = await _customerRepository.GetCustomerByIdAsync(id);
            if (daoCustomer == null)
            {
                return null;
            }

            var domainCustomer = new CustomerDomain(
                daoCustomer.Id,
                daoCustomer.Name,
                daoCustomer.Email,
                _dataEncryptionService
            );
            domainCustomer.SetEncryptedEmailDataFromPersistence(daoCustomer.Email);
            return domainCustomer;
        }

        public async Task AddCustomerAsync(CustomerDomain customer)
        {
            _logger.Info($"新增客戶 Domain 實體: {customer.Name}");

            try
            {
                string currentUnifiedName = "DefaultUnifiedName";

                // 如果 Email 屬性已設定明文，則進行加密
                if (!string.IsNullOrEmpty(customer.Email) && string.IsNullOrEmpty(customer.GetEncryptedEmailDataForPersistence()))
                {
                    var encryptedEmail = _dataEncryptionService.Encrypt(customer.Email, currentUnifiedName);
                    customer.UpdateEncryptedEmailDataForMigration(encryptedEmail);
                }

                var daoCustomer = new Dao.Models.Customer
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    Email = customer.GetEncryptedEmailDataForPersistence(),
                    CreatedDate = DateTime.UtcNow
                };

                await _customerRepository.AddCustomerAsync(daoCustomer);
                await _customerRepository.SaveChangesAsync();

                if (daoCustomer.Id != customer.Id)
                {
                    // 這裡無法直接修改 private set 的 Id，可能需要 CustomerDomain 提供一個方法來更新 ID
                    // 或者在應用層將更新後的 daoCustomer.Id 反饋回 domainCustomer
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"新增客戶失敗: {customer.Name}");
                throw;
            }
        }

        public async Task UpdateCustomerAsync(CustomerDomain customer)
        {
            _logger.Info($"更新客戶 Domain 實體: {customer.Name} (ID: {customer.Id})");

            try
            {
                var daoCustomer = await _customerRepository.GetCustomerByIdAsync(customer.Id);
                if (daoCustomer == null)
                {
                    _logger.Warn($"嘗試更新不存在的客戶 (ID: {customer.Id})。");
                    return;
                }

                daoCustomer.Name = customer.Name;

                string currentUnifiedName = "DefaultUnifiedName";

                if (string.IsNullOrEmpty(customer.GetEncryptedEmailDataForPersistence()) && !string.IsNullOrEmpty(customer.Email))
                {
                    // Email 屬性被設定為明文，需要用最新金鑰重新加密
                    var encryptedEmail = _dataEncryptionService.Encrypt(customer.Email, currentUnifiedName);
                    customer.UpdateEncryptedEmailDataForMigration(encryptedEmail);
                }

                daoCustomer.Email = customer.GetEncryptedEmailDataForPersistence();
                // CreatedDate 通常不會在這裡更新，視業務邏輯而定

                await _customerRepository.UpdateCustomerAsync(daoCustomer);
                await _customerRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"更新客戶失敗: {customer.Name} (ID: {customer.Id})");
                throw;
            }
        }

        public async Task DeleteCustomerAsync(int id)
        {
            _logger.Info($"刪除 ID 為 {id} 的客戶 Domain 實體。");
            try
            {
                await _customerRepository.DeleteCustomerAsync(id);
                await _customerRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"刪除客戶失敗 (ID: {id})");
                throw;
            }
        }
    }
}