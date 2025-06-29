using KeyManagerTool.Models;
using NLog;
using System.Text.RegularExpressions;

public class KeyManagerService
{
    private readonly string _basePath;
    private readonly string updatePath;
    private readonly string currentPath;
    private readonly string historyPath;

    private int serialCounter = 1;

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

        try
        {
            // 這些目錄在應用程式啟動時就應該存在或被建立
            Directory.CreateDirectory(_basePath);
            Directory.CreateDirectory(updatePath);
            Directory.CreateDirectory(currentPath);
            Directory.CreateDirectory(historyPath);

            _logger.Info($"確保金鑰目錄結構存在：{_basePath}, {updatePath}, {currentPath}, {historyPath}");
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, $"無法建立金鑰管理目錄結構。應用程式無法正常啟動：{_basePath}");

            // 如果目錄無法建立，服務無法正常運作，應讓外部（Program.cs）處理終止
            throw;
        }

        try
        {
            _watcher = new FileSystemWatcher(updatePath);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite;

            _watcher.Created += OnFileCreatedOrChanged;
            _watcher.Changed += OnFileCreatedOrChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Deleted += OnFileDeleted;

            _logger.Info($"FileSystemWatcher 已設定並監控目錄：{updatePath}");
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, $"無法初始化 FileSystemWatcher 監控 {updatePath}。服務無法正常啟動。");

            throw; // 嚴重錯誤，讓外部處理
        }
    }

    public Task StartAsync()
    {
        _logger.Info($"開始監控資料夾: {updatePath}");

        ProcessUpdateFolder(); // 首次啟動時掃描一次

        try
        {
            _watcher.EnableRaisingEvents = true;
            _logger.Info($"FileSystemWatcher 已啟用監控：{updatePath}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"無法啟用 FileSystemWatcher 的事件觸發。監控將無效。");
            // 這裡不 throw，讓程式繼續，但監控功能將失效
        }

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
        try
        {
            lock (_folderProcessingLock)
            {
                ProcessUpdateFolder();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "處理 Update 資料夾時發生未預期錯誤。");

            // 不 rethrow，允許程式繼續運行以處理其他潛在事件
        }
    }

    private void ProcessUpdateFolder()
    {
        _logger.Info("掃描 update 資料夾以處理新金鑰組...");

        List<string> filesInUpdate;

        try
        {
            filesInUpdate = Directory.GetFiles(updatePath).ToList();
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Error(ex, $"Update 資料夾不存在: {updatePath}。無法掃描。");

            return;
        }
        catch (IOException ex)
        {
            _logger.Error(ex, $"讀取 Update 資料夾時發生 IO 錯誤: {updatePath}。");

            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, $"沒有權限存取 Update 資料夾: {updatePath}。");

            return;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"掃描 Update 資料夾時發生未預期錯誤: {updatePath}。");

            return;
        }

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
                    try
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
                    catch (FileNotFoundException ex)
                    {
                        _logger.Warn(ex, $"金鑰組 {group.UnifiedName} 中的 AES 檔案不存在，跳過此組。");
                    }
                    catch (IOException ex)
                    {
                        _logger.Warn(ex, $"無法取得金鑰組 {group.UnifiedName} 中 AES 檔案的建立時間，跳過此組。");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.Warn(ex, $"沒有權限取得金鑰組 {group.UnifiedName} 中 AES 檔案的建立時間，跳過此組。");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"處理金鑰組 {group.UnifiedName} 時發生未預期錯誤，跳過此組。");
                    }
                }
                else
                {
                    _logger.Warn($"偵測到名稱不一致的金鑰組，已忽略: {group.UnifiedName}。請檢查檔案命名規則。");
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
        _logger.Info($"處理混合金鑰 {unifiedName} 的 AES 和 RSA 組件...");

        ProcessAesKey(aesPath, unifiedName);
        ProcessRsaKey(rsaPubPath, rsaPrivPath, unifiedName);
    }

    private void ProcessAesKey(string aesFilePath, string overrideName = null)
    {
        var fileName = overrideName ?? Path.GetFileNameWithoutExtension(aesFilePath);
        var currentAes = Directory.GetFiles(currentPath, "*.der").FirstOrDefault();

        // 移動舊的 AES 金鑰到 History
        try
        {
            if (currentAes != null)
            {
                var destHistPath = Path.Combine(historyPath, Path.GetFileName(currentAes));

                File.Move(currentAes, destHistPath, true);

                _logger.Info($"已將舊的 AES 金鑰從 Current 移至 History: {Path.GetFileName(currentAes)}");
            }
            else
            {
                _logger.Info("Current 資料夾中沒有舊的 AES 金鑰，直接搬移新金鑰。");
            }
        }
        catch (FileNotFoundException ex)
        {
            _logger.Warn(ex, $"移動舊 AES 金鑰時，檔案未找到: {currentAes}。可能已被刪除。");
        }
        catch (IOException ex)
        {
            _logger.Error(ex, $"移動舊 AES 金鑰 {currentAes} 到 History 失敗 (IO 錯誤)。");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, $"移動舊 AES 金鑰 {currentAes} 到 History 失敗 (權限不足)。");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"移動舊 AES 金鑰 {currentAes} 到 History 時發生未預期錯誤。");
        }

        // 移動新的 AES 金鑰到 Current
        var destCurrentPath = Path.Combine(currentPath, fileName + ".der");

        try
        {
            File.Move(aesFilePath, destCurrentPath, true);

            _logger.Info($"新 AES 金鑰已生成並儲存到 Current. FileName: {fileName}.der");
        }
        catch (FileNotFoundException ex)
        {
            _logger.Error(ex, $"移動新 AES 金鑰時，來源檔案 {aesFilePath} 未找到。無法完成處理。");

            return; // 嚴重錯誤，無法移動新金鑰
        }
        catch (IOException ex)
        {
            _logger.Error(ex, $"移動新 AES 金鑰 {aesFilePath} 到 Current 失敗 (IO 錯誤)。");

            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, $"移動新 AES 金鑰 {aesFilePath} 到 Current 失敗 (權限不足)。");

            return;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"移動新 AES 金鑰 {aesFilePath} 到 Current 時發生未預期錯誤。");

            return;
        }

        // 確保 current 資料夾中只保留最新的 AES 金鑰
        foreach (var file in Directory.GetFiles(currentPath, "*.der"))
        {
            if (Path.GetFileName(file) != Path.GetFileName(destCurrentPath))
            {
                try
                {
                    File.Delete(file); _logger.Warn($"清除 Current 資料夾中殘留的舊 AES 金鑰: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"清除殘留 AES 金鑰失敗: {Path.GetFileName(file)}");
                }
            }
        }
    }

    private void ProcessRsaKey(string pubPath, string privPath, string overrideName = null)
    {
        var fileName = overrideName ?? Path.GetFileNameWithoutExtension(pubPath).Split('.')[0];
        var currentPub = Directory.GetFiles(currentPath, "*.public.pem").FirstOrDefault();
        var currentPriv = Directory.GetFiles(currentPath, "*.private.pem").FirstOrDefault();

        // 移動舊的 RSA 金鑰到 History
        try
        {
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
        }
        catch (FileNotFoundException ex)
        {
            _logger.Warn(ex, $"移動舊 RSA 金鑰對時，檔案未找到: {currentPub}, {currentPriv}。可能已被刪除。");
        }
        catch (IOException ex)
        {
            _logger.Error(ex, $"移動舊 RSA 金鑰對 {currentPub}, {currentPriv} 到 History 失敗 (IO 錯誤)。");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, $"移動舊 RSA 金鑰對 {currentPub}, {currentPriv} 到 History 失敗 (權限不足)。");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"移動舊 RSA 金鑰對 {currentPub}, {currentPriv} 到 History 時發生未預期錯誤。");
        }

        // 移動新的 RSA 金鑰到 Current
        string destPubPath = Path.Combine(currentPath, fileName + ".public.pem");
        string destPrivPath = Path.Combine(currentPath, fileName + ".private.pem");
        try
        {
            File.Move(pubPath, destPubPath, true);
            File.Move(privPath, destPrivPath, true);

            _logger.Info($"新 RSA 公鑰已生成並儲存到 Current. FileName: {fileName}.public.pem");
            _logger.Info($"新 RSA 私鑰已生成並儲存到 Current. FileName: {fileName}.private.pem");
        }
        catch (FileNotFoundException ex)
        {
            _logger.Error(ex, $"移動新 RSA 金鑰對時，來源檔案 {pubPath}, {privPath} 未找到。無法完成處理。");

            return;
        }
        catch (IOException ex)
        {
            _logger.Error(ex, $"移動新 RSA 金鑰對 {pubPath}, {privPath} 到 Current 失敗 (IO 錯誤)。");

            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, $"移動新 RSA 金鑰對 {pubPath}, {privPath} 到 Current 失敗 (權限不足)。");

            return;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"移動新 RSA 金鑰對 {pubPath}, {privPath} 到 Current 時發生未預期錯誤。");

            return;
        }

        foreach (var file in Directory.GetFiles(currentPath, "*.public.pem").Concat(Directory.GetFiles(currentPath, "*.private.pem")))
        {
            if (!file.Contains(fileName))
            {
                try
                {
                    File.Delete(file); _logger.Warn($"清除 Current 資料夾中殘留的舊 RSA 金鑰: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"清除殘留 RSA 金鑰失敗: {Path.GetFileName(file)}");
                }
            }
        }
    }

    private bool IsAesKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.der$");

    private bool IsRsaPublicKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.public\\.pem$");

    private bool IsRsaPrivateKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.private\\.pem$");
}