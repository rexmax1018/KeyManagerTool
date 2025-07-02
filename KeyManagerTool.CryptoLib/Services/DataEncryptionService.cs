using CryptoSuite.KeyManagement.Enums;
using CryptoSuite.KeyManagement.Models;
using CryptoSuite.Services.Interfaces;
using KeyManagerTool.CryptoLib.Interfaces;
using NLog;
using System.Text;
using System;
using System.IO;

namespace KeyManagerTool.CryptoLib.Services
{
    public class DataEncryptionService : IDataEncryptionService
    {
        private readonly ICryptoService _cryptoService;
        private readonly ICryptoKeyService _cryptoKeyService;
        private readonly KeyManagerService _keyManagerService;
        private readonly ILogger _logger;

        public DataEncryptionService(
            ICryptoService cryptoService,
            ICryptoKeyService cryptoKeyService,
            KeyManagerService keyManagerService,
            ILogger logger)
        {
            _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
            _cryptoKeyService = cryptoKeyService ?? throw new ArgumentNullException(nameof(cryptoKeyService));
            _keyManagerService = keyManagerService ?? throw new ArgumentNullException(nameof(keyManagerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string Encrypt(string plainText, string unifiedName)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                _logger.Warn("嘗試加密空或 null 的明文字串。");

                return null;
            }
            if (string.IsNullOrEmpty(unifiedName))
            {
                _logger.Error("用於加密的 unifiedName 不能為空。");

                throw new ArgumentNullException(nameof(unifiedName), "用於加密的 unifiedName 必須提供。");
            }
            if (unifiedName.Contains("."))
            {
                _logger.Error($"unifiedName '{unifiedName}' 包含不允許的點號 '.'。");

                throw new ArgumentException("unifiedName 不能包含點號 '.'，因為它用於分隔符。", nameof(unifiedName));
            }

            _logger.Debug($"開始加密數據，unifiedName: {unifiedName}");

            RsaKeyModel rsaPublicKeyModel;

            try
            {
                var keySetInfo = _keyManagerService.GetCurrentKeySetInfo(unifiedName);

                if (keySetInfo == null)
                {
                    _logger.Error($"找不到用於 unifiedName '{unifiedName}' 的當前金鑰組信息，無法加密數據。");

                    throw new InvalidOperationException($"找不到 unifiedName '{unifiedName}' 的當前金鑰組。");
                }
                rsaPublicKeyModel = new RsaKeyModel
                {
                    PublicKey = File.ReadAllText(keySetInfo.RsaPublicKeyPath, Encoding.UTF8)
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"直接載入 unifiedName '{unifiedName}' 的 RSA 公鑰 PEM 時發生錯誤。");

                throw new InvalidOperationException($"無法載入 RSA 公鑰以加密數據：{ex.Message}", ex);
            }

            var aesKey = _cryptoKeyService.GenerateKeyOnly<SymmetricKeyModel>(CryptoAlgorithmType.AES);

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedDataBytes;

            try
            {
                encryptedDataBytes = _cryptoService.Encrypt(plainBytes, CryptoAlgorithmType.AES, aesKey);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"使用生成的 AES Key 加密數據時發生錯誤。");

                throw new InvalidOperationException($"數據加密失敗：{ex.Message}", ex);
            }

            var combinedAesKeyAndIv = $"{Convert.ToBase64String(aesKey.Key)}.{Convert.ToBase64String(aesKey.IV)}";
            var combinedAesKeyAndIvBytes = Encoding.UTF8.GetBytes(combinedAesKeyAndIv);
            var encryptedAesKeyAndIvBytes = _cryptoService.Encrypt(combinedAesKeyAndIvBytes, CryptoAlgorithmType.RSA, rsaPublicKeyModel);

            var encryptedDataPart = Convert.ToBase64String(encryptedDataBytes);
            var encryptedAesKeyIvPart = Convert.ToBase64String(encryptedAesKeyAndIvBytes);

            var combinedEncryptedDataAndKey = $"{encryptedDataPart}::{encryptedAesKeyIvPart}";

            var result = $"{combinedEncryptedDataAndKey}.{unifiedName}";

            _logger.Info($"數據加密成功，長度：{result.Length}，unifiedName: {unifiedName}");

            return result;
        }

        public string Decrypt(string encryptedDataWithUnifiedName)
        {
            if (string.IsNullOrEmpty(encryptedDataWithUnifiedName))
            {
                _logger.Warn("嘗試解密空或 null 的加密字串。");

                return null;
            }

            string unifiedName;
            string combinedEncryptedDataAndKey;

            try
            {
                var lastDotIndex = encryptedDataWithUnifiedName.LastIndexOf('.');

                if (lastDotIndex == -1 || lastDotIndex == encryptedDataWithUnifiedName.Length - 1)
                {
                    _logger.Error($"解密字串格式不正確：'{encryptedDataWithUnifiedName}'。預期格式：Base64數據.unifiedName。");

                    throw new ArgumentException("加密字串格式不正確，缺少有效的 unifiedName 尾綴。", nameof(encryptedDataWithUnifiedName));
                }

                combinedEncryptedDataAndKey = encryptedDataWithUnifiedName[..lastDotIndex];
                unifiedName = encryptedDataWithUnifiedName[(lastDotIndex + 1)..];
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"解析加密字串格式失敗：'{encryptedDataWithUnifiedName}'。");

                throw new ArgumentException("無法解析加密字串的 unifiedName 和密文部分。", nameof(encryptedDataWithUnifiedName), ex);
            }

            _logger.Debug($"開始解密數據，unifiedName: {unifiedName}");

            var separatorIndex = combinedEncryptedDataAndKey.IndexOf("::");

            if (separatorIndex == -1)
            {
                _logger.Error($"解密失敗：複合加密數據格式不正確：'{combinedEncryptedDataAndKey}'。預期包含 '::' 分隔符。");

                throw new ArgumentException("複合加密數據格式不正確，缺少數據和金鑰部分的分隔符。", nameof(encryptedDataWithUnifiedName));
            }

            var encryptedDataPart = combinedEncryptedDataAndKey[..separatorIndex];
            var encryptedAesKeyIvPart = combinedEncryptedDataAndKey[(separatorIndex + 2)..];

            RsaKeyModel rsaPrivateKeyModel;
            SymmetricKeyModel aesKey;

            try
            {
                var keySetInfo = _keyManagerService.GetCurrentKeySetInfo(unifiedName);

                keySetInfo ??= _keyManagerService.GetHistoryKeySetInfo(unifiedName);

                if (keySetInfo == null)
                {
                    _logger.Error($"解密數據失敗：找不到用於 unifiedName '{unifiedName}' 的金鑰組信息 (current 和 history 中均未找到)。");

                    throw new InvalidOperationException($"找不到用於 unifiedName '{unifiedName}' 的金鑰組。");
                }

                rsaPrivateKeyModel = new RsaKeyModel
                {
                    PrivateKey = File.ReadAllText(keySetInfo.RsaPrivateKeyPath, Encoding.UTF8)
                };

                byte[] encryptedAesKeyIvBytes = Convert.FromBase64String(encryptedAesKeyIvPart);

                aesKey = ParseEncryptedSymmetricKey(encryptedAesKeyIvBytes, rsaPrivateKeyModel);

                if (aesKey == null)
                {
                    _logger.Error($"解密 AES Key+IV 失敗，unifiedName: {unifiedName}。");

                    throw new InvalidOperationException($"無法解密 AES Key+IV。");
                }
            }
            catch (FormatException ex)
            {
                _logger.Error(ex, $"解密失敗：加密的 AES Key+IV Base64 部分無效：'{encryptedAesKeyIvPart}'。");

                throw new ArgumentException("加密的 AES Key+IV Base64 部分格式無效。", nameof(encryptedDataWithUnifiedName), ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"獲取或解密 unifiedName '{unifiedName}' 的 RSA 私鑰或 AES Key+IV 時發生錯誤。");

                throw new InvalidOperationException($"無法獲取或解密金鑰以解密數據：{ex.Message}", ex);
            }

            byte[] encryptedDataBytes;

            try
            {
                encryptedDataBytes = Convert.FromBase64String(encryptedDataPart);
            }
            catch (FormatException ex)
            {
                _logger.Error(ex, $"解密失敗：實際數據 Base64 部分無效：'{encryptedDataPart}'。");

                throw new ArgumentException("實際數據的 Base64 部分格式無效。", nameof(encryptedDataWithUnifiedName), ex);
            }

            byte[] decryptedBytes;
            try
            {
                decryptedBytes = _cryptoService.Decrypt(encryptedDataBytes, CryptoAlgorithmType.AES, aesKey);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"使用 AES Key '{unifiedName}' 解密實際數據時發生錯誤。");

                throw new InvalidOperationException($"實際數據解密失敗：{ex.Message}", ex);
            }

            var plainText = Encoding.UTF8.GetString(decryptedBytes);

            _logger.Info($"數據解密成功，unifiedName: {unifiedName}");

            return plainText;
        }

        public string GetUnifiedNameFromEncryptedData(string encryptedDataWithUnifiedName)
        {
            if (string.IsNullOrEmpty(encryptedDataWithUnifiedName))
            {
                _logger.Warn("嘗試從空或 null 的字串中提取 unifiedName。");

                return null;
            }

            int lastDotIndex = encryptedDataWithUnifiedName.LastIndexOf('.');

            if (lastDotIndex == -1 || lastDotIndex == encryptedDataWithUnifiedName.Length - 1)
            {
                _logger.Error($"提取 unifiedName 失敗：字串格式不正確：'{encryptedDataWithUnifiedName}'。預期格式：Base64數據.unifiedName。");

                throw new ArgumentException("加密字串格式不正確，缺少有效的 unifiedName 尾綴。", nameof(encryptedDataWithUnifiedName));
            }

            return encryptedDataWithUnifiedName[(lastDotIndex + 1)..];
        }

        private SymmetricKeyModel? ParseEncryptedSymmetricKey(byte[] encryptedBytes, RsaKeyModel rsaKey)
        {
            try
            {
                var decrypted = _cryptoService.Decrypt(encryptedBytes, CryptoAlgorithmType.RSA, rsaKey);
                var text = Encoding.UTF8.GetString(decrypted);
                var parts = text.Split('.');

                if (parts.Length != 2)
                    return null;

                var keyBytes = Convert.FromBase64String(parts[0]);
                var ivBytes = Convert.FromBase64String(parts[1]);

                return new SymmetricKeyModel
                {
                    Key = keyBytes,
                    IV = ivBytes
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "解密加密的 SymmetricKey (AES Key+IV) 時發生錯誤。");

                return null;
            }
        }
    }
}