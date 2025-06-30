namespace KeyManagerTool.Services.Models
{
    public class KeySetInfo
    {
        public string UnifiedName { get; set; }
        public string AesPath { get; set; }
        public string RsaPublicKeyPath { get; set; }
        public string RsaPrivateKeyPath { get; set; }
        public DateTime CreationTime { get; set; } // 用於排序

        public string[] GetAllPaths() => new[] { AesPath, RsaPublicKeyPath, RsaPrivateKeyPath };
    }
}