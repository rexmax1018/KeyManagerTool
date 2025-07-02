using KeyManagerTool.CryptoLib.Interfaces;

namespace KeyManagerTool.Domain
{
    public class CustomerDomain
    {
        public int Id { get; private set; }
        public string Name { get; private set; }

        private string _encryptedEmailData;

        private string _emailPlainText;

        private readonly IDataEncryptionService _dataEncryptionService;

        public string Email
        {
            get
            {
                if (_dataEncryptionService == null)
                {
                    throw new InvalidOperationException("IDataEncryptionService 未被注入到 CustomerDomain  Domain 實體中，無法解密 Email。");
                }

                if (!string.IsNullOrEmpty(_encryptedEmailData))
                {
                    return _dataEncryptionService.Decrypt(_encryptedEmailData);
                }

                return _emailPlainText;
            }
            set
            {
                _emailPlainText = value;
                _encryptedEmailData = null;
            }
        }

        public CustomerDomain(int id, string name, string encryptedEmailData, IDataEncryptionService dataEncryptionService)
        {
            Id = id;
            Name = name;
            _dataEncryptionService = dataEncryptionService ?? throw new ArgumentNullException(nameof(dataEncryptionService));
            _encryptedEmailData = encryptedEmailData;
            _emailPlainText = null;
        }

        public static CustomerDomain CreateNew(int id, string name, string email, IDataEncryptionService dataEncryptionService, string currentUnifiedNameForNewEncryption)
        {
            if (dataEncryptionService == null)
            {
                throw new ArgumentNullException(nameof(dataEncryptionService), "創建新客戶時，必須提供 IDataEncryptionService。");
            }

            if (string.IsNullOrEmpty(currentUnifiedNameForNewEncryption))
            {
                throw new ArgumentException("創建新客戶時，必須提供 currentUnifiedNameForNewEncryption。", nameof(currentUnifiedNameForNewEncryption));
            }

            var encryptedEmail = dataEncryptionService.Encrypt(email, currentUnifiedNameForNewEncryption);

            return new CustomerDomain(id, name, encryptedEmail, dataEncryptionService);
        }

        public void SetEncryptedEmailDataFromPersistence(string encryptedDataFromDb)
        {
            _encryptedEmailData = encryptedDataFromDb;
            _emailPlainText = null;
        }

        public string GetEncryptedEmailDataForPersistence()
        {
            if (!string.IsNullOrEmpty(_encryptedEmailData))
            {
                return _encryptedEmailData;
            }

            throw new InvalidOperationException("Email 屬性已被設定為明文但尚未被加密，無法獲取持久化數據。請確保在持久化前已通過應用服務更新加密數據。");
        }

        public void UpdateEncryptedEmailDataForMigration(string newEncryptedDataWithUnifiedName)
        {
            if (string.IsNullOrEmpty(newEncryptedDataWithUnifiedName))
            {
                throw new ArgumentException("新的加密數據不能為空。", nameof(newEncryptedDataWithUnifiedName));
            }

            _encryptedEmailData = newEncryptedDataWithUnifiedName;
            _emailPlainText = null;
        }
    }
}