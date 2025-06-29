using KeyManagerTool.Models;
using System.Text.RegularExpressions;

public class KeyManagerService
{
    private readonly string _basePath;
    private readonly string updatePath;
    private readonly string currentPath;
    private readonly string historyPath;
    private FileSystemWatcher _watcher;
    private readonly object _folderProcessingLock = new object();
    private readonly NLog.ILogger _logger;

    public KeyManagerService(NLog.ILogger logger, string basePath)
    {
        _logger = logger;
        _basePath = basePath;

        updatePath = Path.Combine(_basePath, "update");
        currentPath = Path.Combine(_basePath, "current");
        historyPath = Path.Combine(_basePath, "history");

        Directory.CreateDirectory(updatePath);
        Directory.CreateDirectory(currentPath);
        Directory.CreateDirectory(historyPath);

        _watcher = new FileSystemWatcher(updatePath);
        _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite;

        _watcher.Created += OnFileCreatedOrChanged;
        _watcher.Changed += OnFileCreatedOrChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Deleted += OnFileDeleted;
    }

    public Task StartAsync()
    {
        _logger.Info($"開始監控資料夾: {updatePath}");

        ProcessUpdateFolder();

        _watcher.EnableRaisingEvents = true;

        return Task.CompletedTask;
    }

    private void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
    {
        _logger.Info($"偵測到檔案變動: {e.FullPath}, 類型: {e.ChangeType}");
        ProcessUpdateFolderWrapper();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.Info($"偵測到檔案更名: {e.OldFullPath} -> {e.FullPath}, 類型: {e.ChangeType}");
        ProcessUpdateFolderWrapper();
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.Info($"偵測到檔案刪除: {e.FullPath}, 類型: {e.ChangeType}");
        ProcessUpdateFolderWrapper();
    }

    private void ProcessUpdateFolderWrapper()
    {
        lock (_folderProcessingLock)
        {
            ProcessUpdateFolder();
        }
    }

    private void ProcessUpdateFolder()
    {
        _logger.Info("掃描 update 資料夾以處理新金鑰組...");

        var filesInUpdate = Directory.GetFiles(updatePath);
        var fileGroups = filesInUpdate
            .GroupBy(f => Path.GetFileNameWithoutExtension(f).Split('.')[0])
            .Select(g => new
            {
                UnifiedName = g.Key,
                Files = g.ToList()
            })
            .ToList();

        var completeKeySets = new List<KeySetInfo>();

        foreach (var group in fileGroups)
        {
            var aesFile = group.Files.FirstOrDefault(f => IsAesKey(Path.GetFileName(f)));
            var rsaPubFile = group.Files.FirstOrDefault(f => IsRsaPublicKey(Path.GetFileName(f)));
            var rsaPrivFile = group.Files.FirstOrDefault(f => IsRsaPrivateKey(Path.GetFileName(f)));

            if (aesFile != null && rsaPubFile != null && rsaPrivFile != null)
            {
                if (Path.GetFileNameWithoutExtension(aesFile) == group.UnifiedName &&
                    Path.GetFileNameWithoutExtension(rsaPubFile).StartsWith(group.UnifiedName) &&
                    Path.GetFileNameWithoutExtension(rsaPrivFile).StartsWith(group.UnifiedName))
                {
                    var creationTime = File.GetCreationTimeUtc(aesFile);
                    completeKeySets.Add(new KeySetInfo
                    {
                        UnifiedName = group.UnifiedName,
                        AesPath = aesFile,
                        RsaPublicKeyPath = rsaPubFile,
                        RsaPrivateKeyPath = rsaPrivFile,
                        CreationTime = creationTime
                    });
                }
                else
                {
                    _logger.Warn($"偵測到名稱不一致的金鑰組，已忽略: {group.UnifiedName}");
                }
            }
            else
            {
                _logger.Warn($"偵測到不完整的金鑰組或殘留檔案，將暫時保留在 Update 資料夾: {group.UnifiedName}");
                foreach (var incompleteFile in group.Files)
                {
                    _logger.Warn($"不完整金鑰組檔案: {Path.GetFileName(incompleteFile)}");
                }
            }
        }

        var sortedKeySets = completeKeySets.OrderBy(ks => ks.CreationTime).ToList();

        foreach (var keySet in sortedKeySets)
        {
            _logger.Info($"開始處理金鑰組: {keySet.UnifiedName} (建立時間: {keySet.CreationTime:o})");
            ProcessHybridKeys(keySet.AesPath, keySet.RsaPublicKeyPath, keySet.RsaPrivateKeyPath, keySet.UnifiedName);

            foreach (var filePath in keySet.GetAllPaths())
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _logger.Info($"已從 Update 資料夾刪除檔案: {Path.GetFileName(filePath)}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"刪除檔案失敗: {Path.GetFileName(filePath)}");
                }
            }
        }
    }

    private void ProcessHybridKeys(string aesPath, string rsaPubPath, string rsaPrivPath, string unifiedName)
    {
        ProcessAesKey(aesPath, unifiedName);
        ProcessRsaKey(rsaPubPath, rsaPrivPath, unifiedName);
    }

    private void ProcessAesKey(string aesFilePath, string overrideName = null)
    {
        string fileName = overrideName ?? Path.GetFileNameWithoutExtension(aesFilePath);
        string currentAes = Directory.GetFiles(currentPath, "*.der").FirstOrDefault();

        if (currentAes != null)
        {
            string destHistPath = Path.Combine(historyPath, Path.GetFileName(currentAes));
            File.Move(currentAes, destHistPath, true);
            _logger.Info($"已將舊的 AES 金鑰從 Current 移至 History: {Path.GetFileName(currentAes)}");
        }
        else
        {
            _logger.Info("Current 資料夾中沒有舊的 AES 金鑰，直接搬移新金鑰。");
        }

        string destCurrentPath = Path.Combine(currentPath, fileName + ".der");
        File.Move(aesFilePath, destCurrentPath, true);

        // 移除 ID
        _logger.Info($"新 AES 金鑰已生成並儲存到 Current. FileName: {fileName}.der");
        // serialCounter++; // 移除此行

        foreach (var file in Directory.GetFiles(currentPath, "*.der"))
        {
            if (Path.GetFileName(file) != Path.GetFileName(destCurrentPath))
            {
                try { File.Delete(file); _logger.Warn($"清除 Current 資料夾中殘留的舊 AES 金鑰: {Path.GetFileName(file)}"); }
                catch (Exception ex) { _logger.Error(ex, $"清除殘留 AES 金鑰失敗: {Path.GetFileName(file)}"); }
            }
        }
    }

    private void ProcessRsaKey(string pubPath, string privPath, string overrideName = null)
    {
        string fileName = overrideName ?? Path.GetFileNameWithoutExtension(pubPath).Split('.')[0];

        string currentPub = Directory.GetFiles(currentPath, "*.public.pem").FirstOrDefault();
        string currentPriv = Directory.GetFiles(currentPath, "*.private.pem").FirstOrDefault();

        if (currentPub != null && currentPriv != null)
        {
            File.Move(currentPub, Path.Combine(historyPath, Path.GetFileName(currentPub)), true);
            File.Move(currentPriv, Path.Combine(historyPath, Path.GetFileName(currentPriv)), true);
            _logger.Info($"已將舊的 RSA 金鑰對從 Current 移至 History: {Path.GetFileName(currentPub)}, {Path.GetFileName(currentPriv)}");
        }
        else
        {
            _logger.Info("Current 資料夾中沒有舊的 RSA 金鑰對，直接搬移新金鑰。");
        }

        File.Move(pubPath, Path.Combine(currentPath, fileName + ".public.pem"), true);
        File.Move(privPath, Path.Combine(currentPath, fileName + ".private.pem"), true);

        // 移除 ID
        _logger.Info($"新 RSA 公鑰已生成並儲存到 Current. FileName: {fileName}.public.pem");
        // serialCounter++; // 移除此行

        _logger.Info($"新 RSA 私鑰已生成並儲存到 Current. FileName: {fileName}.private.pem");
        // serialCounter++; // 移除此行

        foreach (var file in Directory.GetFiles(currentPath, "*.public.pem").Concat(Directory.GetFiles(currentPath, "*.private.pem")))
        {
            if (!file.Contains(fileName))
            {
                try { File.Delete(file); _logger.Warn($"清除 Current 資料夾中殘留的舊 RSA 金鑰: {Path.GetFileName(file)}"); }
                catch (Exception ex) { _logger.Error(ex, $"清除殘留 RSA 金鑰失敗: {Path.GetFileName(file)}"); }
            }
        }
    }

    private bool IsAesKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.der$");

    private bool IsRsaPublicKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.public\\.pem$");

    private bool IsRsaPrivateKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.private\\.pem$");
}