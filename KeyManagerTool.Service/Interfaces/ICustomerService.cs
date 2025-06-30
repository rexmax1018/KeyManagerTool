using KeyManagerTool.Domain;

namespace KeyManagerTool.Service.Interfaces
{
    public interface ICustomerService
    {
        Task<List<CustomerDomain>> GetAllCustomersAsync();

        Task<CustomerDomain> GetCustomerByIdAsync(int id);

        Task AddCustomerAsync(CustomerDomain customer);

        Task UpdateCustomerAsync(CustomerDomain customer);

        Task DeleteCustomerAsync(int id);
    }
}