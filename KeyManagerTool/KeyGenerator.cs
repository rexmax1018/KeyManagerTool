using CryptoSuite.KeyManagement.Enums;
using CryptoSuite.KeyManagement.Models;
using CryptoSuite.Services.Interfaces;
using System.Text;

namespace KeyManagerTool
{
    public class KeyGenerator
    {
        private readonly ICryptoKeyService _keyService;
        private readonly ICryptoService _cryptoService;
        private readonly string _basePath; // 新增此成員變數來儲存傳入的 basePath

        // 修改建構函式，接受 basePath 參數
        public KeyGenerator(ICryptoKeyService keyService, ICryptoService cryptoService, string basePath)
        {
            _keyService = keyService;
            _cryptoService = cryptoService;
            _basePath = basePath; // 初始化 _basePath
        }

        public void Generate()
        {
            // 使用 _basePath 來構建 updatePath
            string updatePath = Path.Combine(_basePath, "update");
            Directory.CreateDirectory(updatePath);

            string unifiedName = GetRandomName();

            // 產生 RSA 金鑰模型
            var rsaKey = _keyService.GenerateKeyOnly<RsaKeyModel>(CryptoAlgorithmType.RSA);
            File.WriteAllText(Path.Combine(updatePath, $"{unifiedName}.public.pem"), rsaKey.PublicKey);
            File.WriteAllText(Path.Combine(updatePath, $"{unifiedName}.private.pem"), rsaKey.PrivateKey);
            Console.WriteLine($"[測試] RSA 金鑰已產生: {unifiedName}.public.pem / .private.pem");

            // 產生 AES 金鑰模型（含 Key + IV）
            var aesKey = _keyService.GenerateKeyOnly<SymmetricKeyModel>(CryptoAlgorithmType.AES);

            // 將 AES key 與 IV 合併為 "{base64(key)}.{base64(iv)}"
            string combined = $"{Convert.ToBase64String(aesKey.Key)}.{Convert.ToBase64String(aesKey.IV)}";
            byte[] combinedBytes = Encoding.UTF8.GetBytes(combined);

            // 使用 RSA 公鑰加密組合內容
            byte[] encrypted = _cryptoService.Encrypt(combinedBytes, CryptoAlgorithmType.RSA, rsaKey);

            // 儲存加密內容為 .der
            string aesDerPath = Path.Combine(updatePath, $"{unifiedName}.der");
            File.WriteAllBytes(aesDerPath, encrypted);
            Console.WriteLine($"[測試] AES Key + IV 已加密儲存: {unifiedName}.der");

            // 驗證：從檔案讀取並解密還原
            byte[] encryptedFromFile = File.ReadAllBytes(aesDerPath);
            byte[] decrypted = _cryptoService.Decrypt(encryptedFromFile, CryptoAlgorithmType.RSA, rsaKey);
            string decryptedText = Encoding.UTF8.GetString(decrypted);

            // 拆解為 key + IV 並比對原始值
            var parts = decryptedText.Split('.');
            if (parts.Length != 2)
            {
                Console.WriteLine("解密後格式錯誤，應為 {AES}.{IV}");
                return;
            }

            byte[] decodedKey = Convert.FromBase64String(parts[0]);
            byte[] decodedIV = Convert.FromBase64String(parts[1]);

            bool keyMatch = aesKey.Key.SequenceEqual(decodedKey);
            bool ivMatch = aesKey.IV.SequenceEqual(decodedIV);

            Console.WriteLine(keyMatch && ivMatch
                ? "解密成功，AES 金鑰與 IV 完全一致"
                : "解密失敗，AES 金鑰或 IV 不一致");
        }

        private static string GetRandomName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}