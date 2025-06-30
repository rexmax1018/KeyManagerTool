namespace KeyManagerTool.Domain.Interfaces
{
    public interface IDataEncryptionService
    {
        /// <summary>
        /// 加密明文字串，並添加包含 unifiedName 的尾綴。
        /// 格式為：encryptedBase64.{unifiedName}
        /// </summary>
        /// <param name="plainText">待加密的明文字串。</param>
        /// <param name="unifiedName">用於加密的金鑰組的統一名稱。</param>
        /// <returns>加密後的字串（包含 unifiedName 尾綴）。</returns>
        string Encrypt(string plainText, string unifiedName);

        /// <summary>
        /// 解密加密字串。
        /// 期望格式為：encryptedBase64.{unifiedName}
        /// </summary>
        /// <param name="encryptedDataWithUnifiedName">包含 unifiedName 尾綴的加密字串。</param>
        /// <returns>解密後的明文字串。</returns>
        /// <exception cref="ArgumentException">如果加密字串格式不正確。</exception>
        /// <exception cref="InvalidOperationException">如果找不到對應的金鑰或解密失敗。</exception>
        string Decrypt(string encryptedDataWithUnifiedName);

        /// <summary>
        /// 從加密字串中提取 unifiedName。
        /// 期望格式為：encryptedBase64.{unifiedName}
        /// </summary>
        /// <param name="encryptedDataWithUnifiedName">包含 unifiedName 尾綴的加密字串。</param>
        /// <returns>提取到的 unifiedName。</returns>
        /// <exception cref="ArgumentException">如果加密字串格式不正確。</exception>
        string GetUnifiedNameFromEncryptedData(string encryptedDataWithUnifiedName);
    }
}