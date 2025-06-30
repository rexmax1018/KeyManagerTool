using CryptoSuite.KeyManagement.Enums;
using CryptoSuite.KeyManagement.Models;
using CryptoSuite.Services.Interfaces;
using NLog;
using System.Security.Cryptography;
using System.Text;

namespace KeyManagerTool.Services
{
    public class KeyGenerator
    {
        private readonly ICryptoKeyService _keyService;
        private readonly ICryptoService _cryptoService;
        private readonly string _basePath;
        private readonly ILogger _logger;

        public KeyGenerator(ICryptoKeyService keyService, ICryptoService cryptoService, string basePath, ILogger logger)
        {
            _keyService = keyService;
            _cryptoService = cryptoService;
            _basePath = basePath;
            _logger = logger;
        }

        public void Generate()
        {
            var updatePath = Path.Combine(_basePath, "update");

            try
            {
                Directory.CreateDirectory(updatePath);

                _logger.Info($"確保金鑰更新目錄存在: {updatePath}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"無法建立金鑰更新目錄: {updatePath}。程式將停止金鑰生成。");

                Console.WriteLine($"錯誤：無法建立目錄 {updatePath}。");

                return;
            }

            var unifiedName = GetRandomName();

            _logger.Info($"開始生成金鑰組: {unifiedName}");

            RsaKeyModel rsaKey = null;

            // 產生 RSA 金鑰模型並儲存
            try
            {
                rsaKey = _keyService.GenerateKeyOnly<RsaKeyModel>(CryptoAlgorithmType.RSA);

                File.WriteAllText(Path.Combine(updatePath, $"{unifiedName}.public.pem"), rsaKey.PublicKey);
                File.WriteAllText(Path.Combine(updatePath, $"{unifiedName}.private.pem"), rsaKey.PrivateKey);

                _logger.Info($"RSA 金鑰已生成並儲存到 {updatePath}: {unifiedName}.public.pem / .private.pem");

                Console.WriteLine($"[測試] RSA 金鑰已產生: {unifiedName}.public.pem / .private.pem");
            }
            catch (CryptographicException ex)
            {
                _logger.Error(ex, "RSA 金鑰生成失敗 (加密錯誤)。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：RSA 金鑰生成失敗。");

                return;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, $"RSA 金鑰檔案寫入失敗到 {updatePath}。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：RSA 金鑰檔案寫入失敗。");

                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error(ex, $"沒有權限寫入 RSA 金鑰檔案到 {updatePath}。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：沒有寫入權限。");

                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "RSA 金鑰生成或儲存時發生未預期錯誤。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：RSA 金鑰操作失敗。");

                return;
            }

            SymmetricKeyModel aesKey = null;

            // 產生 AES 金鑰模型（含 Key + IV），加密並儲存
            try
            {
                aesKey = _keyService.GenerateKeyOnly<SymmetricKeyModel>(CryptoAlgorithmType.AES);

                var combined = $"{Convert.ToBase64String(aesKey.Key)}.{Convert.ToBase64String(aesKey.IV)}";
                var combinedBytes = Encoding.UTF8.GetBytes(combined);
                var encrypted = _cryptoService.Encrypt(combinedBytes, CryptoAlgorithmType.RSA, rsaKey);
                var aesDerPath = Path.Combine(updatePath, $"{unifiedName}.der");

                File.WriteAllBytes(aesDerPath, encrypted);

                _logger.Info($"AES Key + IV 已加密儲存到 {updatePath}: {unifiedName}.der");

                Console.WriteLine($"[測試] AES Key + IV 已加密儲存: {unifiedName}.der");
            }
            catch (CryptographicException ex)
            {
                _logger.Error(ex, "AES 金鑰生成、加密或儲存失敗 (加密錯誤)。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：AES 金鑰操作失敗。");

                return;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, $"AES 金鑰檔案寫入失敗到 {updatePath}。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：AES 金鑰檔案寫入失敗。");

                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error(ex, $"沒有權限寫入 AES 金鑰檔案到 {updatePath}。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：沒有寫入權限。");

                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "AES 金鑰生成、加密或儲存時發生未預期錯誤。程式將停止金鑰生成。");

                Console.WriteLine("錯誤：AES 金鑰操作失敗。");

                return;
            }

            // 驗證：從檔案讀取並解密還原
            var aesDerPathForValidation = Path.Combine(updatePath, $"{unifiedName}.der");

            try
            {
                var encryptedFromFile = File.ReadAllBytes(aesDerPathForValidation);
                var decrypted = _cryptoService.Decrypt(encryptedFromFile, CryptoAlgorithmType.RSA, rsaKey);
                var decryptedText = Encoding.UTF8.GetString(decrypted);
                var parts = decryptedText.Split('.');

                if (parts.Length != 2)
                {
                    Console.WriteLine("解密後格式錯誤，應為 {AES}.{IV}");

                    _logger.Error($"解密後的 AES 金鑰格式錯誤: {decryptedText}。驗證失敗。");

                    return;
                }

                var decodedKey = Convert.FromBase64String(parts[0]);
                var decodedIV = Convert.FromBase64String(parts[1]);
                var keyMatch = aesKey.Key.SequenceEqual(decodedKey);
                var ivMatch = aesKey.IV.SequenceEqual(decodedIV);

                Console.WriteLine(keyMatch && ivMatch ? "解密成功，AES 金鑰與 IV 完全一致" : "解密失敗，AES 金鑰或 IV 不一致");

                if (!(keyMatch && ivMatch))
                {
                    _logger.Error("AES 金鑰或 IV 解密後不一致。驗證失敗。");
                }
                else
                {
                    _logger.Info("AES 金鑰和 IV 成功解密並驗證一致。");
                }
            }
            catch (FileNotFoundException ex)
            {
                _logger.Error(ex, $"解密驗證時檔案未找到: {aesDerPathForValidation}。驗證失敗。");

                Console.WriteLine("錯誤：解密驗證檔案未找到。");

                return;
            }
            catch (CryptographicException ex)
            {
                _logger.Error(ex, "金鑰解密失敗 (加密錯誤)。驗證失敗。");

                Console.WriteLine("錯誤：金鑰解密失敗。");

                return;
            }
            catch (FormatException ex)
            {
                _logger.Error(ex, "Base64 解碼失敗。驗證失敗。");

                Console.WriteLine("錯誤：Base64 解碼失敗。");

                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "金鑰解密或驗證時發生未預期錯誤。驗證失敗。");

                Console.WriteLine("錯誤：金鑰解密或驗證失敗。");

                return;
            }

            _logger.Info($"金鑰組 {unifiedName} 生成並驗證完成。");
        }

        private static string GetRandomName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();

            return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}