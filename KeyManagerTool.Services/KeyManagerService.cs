using KeyManagerTool.Services.Models;
using System.Text.RegularExpressions;

public class KeyManagerService
{
    private readonly string _basePath;
    private readonly string updatePath;
    private readonly string currentPath;
    private readonly string historyPath;

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

            _logger.Info($"確保金鑰目錄結構存在：{_basePath}, {updatePath}, {currentPath}, {historyPath}"); //
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, $"無法建立金鑰管理目錄結構。應用程式無法正常啟動：{_basePath}"); //
            throw;
        }

        // 由於專案改為 Windows 排程執行一次，FileSystemWatcher 不再需要。
        // 原有 FileSystemWatcher 的初始化程式碼已移除。
        _logger.Info("FileSystemWatcher 已移除，因為此應用程式將透過 Windows 排程單次執行。");
    }

    // 將 StartAsync 修改為只執行一次性處理
    public Task StartAsync()
    {
        _logger.Info($"開始執行一次性金鑰處理..."); //

        ProcessUpdateFolderWrapper(); // 執行一次資料夾處理 //

        _logger.Info($"一次性金鑰處理完成。");
        return Task.CompletedTask;
    }

    private void ProcessUpdateFolderWrapper()
    {
        try
        {
            lock (_folderProcessingLock)
            {
                ProcessUpdateFolder(); //
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "處理 Update 資料夾時發生未預期錯誤。"); //
        }
    }

    private void ProcessUpdateFolder()
    {
        _logger.Info("掃描 update 資料夾以處理新金鑰組..."); //

        List<string> filesInUpdate;

        try
        {
            filesInUpdate = Directory.GetFiles(updatePath).ToList(); //
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Error(ex, $"Update 資料夾不存在: {updatePath}。無法掃描。"); //
            return;
        }
        catch (IOException ex)
        {
            _logger.Error(ex, $"讀取 Update 資料夾時發生 IO 錯誤: {updatePath}。"); //
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, $"沒有權限存取 Update 資料夾: {updatePath}。"); //
            return;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"掃描 Update 資料夾時發生未預期錯誤: {updatePath}。"); //
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
            var aesFile = group.Files.FirstOrDefault(f => IsAesKey(Path.GetFileName(f))); //
            var rsaPubFile = group.Files.FirstOrDefault(f => IsRsaPublicKey(Path.GetFileName(f))); //
            var rsaPrivFile = group.Files.FirstOrDefault(f => IsRsaPrivateKey(Path.GetFileName(f))); //

            if (aesFile != null && rsaPubFile != null && rsaPrivFile != null) //
            {
                if (Path.GetFileNameWithoutExtension(aesFile).Split('.')[0] == group.UnifiedName && //
                    Path.GetFileNameWithoutExtension(rsaPubFile).StartsWith(group.UnifiedName) && //
                    Path.GetFileNameWithoutExtension(rsaPrivFile).StartsWith(group.UnifiedName)) //
                {
                    try
                    {
                        var creationTime = File.GetCreationTimeUtc(aesFile); //

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
                        _logger.Warn(ex, $"金鑰組 {group.UnifiedName} 中的 AES 檔案不存在，跳過此組。"); //
                    }
                    catch (IOException ex)
                    {
                        _logger.Warn(ex, $"無法取得金鑰組 {group.UnifiedName} 中 AES 檔案的建立時間，跳過此組。"); //
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.Warn(ex, $"沒有權限取得金鑰組 {group.UnifiedName} 中 AES 檔案的建立時間，跳過此組。"); //
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"處理金鑰組 {group.UnifiedName} 時發生未預期錯誤，跳過此組。"); //
                    }
                }
                else
                {
                    _logger.Warn($"偵測到名稱不一致的金鑰組，已忽略: {group.UnifiedName}。請檢查檔案命名規則。"); //
                }
            }
            else
            {
                _logger.Warn($"偵測到不完整的金鑰組或殘留檔案，將暫時保留在 Update 資料夾: {group.UnifiedName}"); //

                foreach (var incompleteFile in group.Files)
                {
                    _logger.Warn($"不完整金鑰組檔案: {Path.GetFileName(incompleteFile)}"); //
                }
            }
        }

        var sortedKeySets = completeKeySets.OrderBy(ks => ks.CreationTime).ToList(); //

        foreach (var keySet in sortedKeySets)
        {
            _logger.Info($"開始處理金鑰組: {keySet.UnifiedName} (建立時間: {keySet.CreationTime:o})"); //

            bool success = ProcessAndMoveKeySet(keySet); //

            if (success) //
            {
                foreach (var filePath in keySet.GetAllPaths()) //
                {
                    try
                    {
                        if (File.Exists(filePath)) //
                        {
                            File.Delete(filePath); //
                            _logger.Info($"已從 Update 資料夾刪除檔案: {Path.GetFileName(filePath)}"); //
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"刪除檔案失敗: {Path.GetFileName(filePath)}"); //
                    }
                }
            }
            else
            {
                _logger.Error($"金鑰組 {keySet.UnifiedName} 未能成功處理，檔案將保留在 Update 資料夾中，等待下次處理或手動介入。"); //
            }
        }
    }

    private bool ProcessAndMoveKeySet(KeySetInfo keySet)
    {
        _logger.Info($"嘗試移動金鑰組 {keySet.UnifiedName} 到 Current 資料夾..."); //

        // 1. 先處理舊金鑰：將 current 資料夾中的舊金鑰移至 history
        try
        {
            // 移動舊的 AES 金鑰到 History
            var currentAes = Directory.GetFiles(currentPath, "*.der").FirstOrDefault(); //
            if (currentAes != null) //
            {
                var destHistPath = Path.Combine(historyPath, Path.GetFileName(currentAes)); //
                File.Move(currentAes, destHistPath, true); //
                _logger.Info($"已將舊的 AES 金鑰從 Current 移至 History: {Path.GetFileName(currentAes)}"); //
            }
            else
            {
                _logger.Info("Current 資料夾中沒有舊的 AES 金鑰。"); //
            }

            // 移動舊的 RSA 金鑰對到 History
            var currentPub = Directory.GetFiles(currentPath, "*.public.pem").FirstOrDefault(); //
            var currentPriv = Directory.GetFiles(currentPath, "*.private.pem").FirstOrDefault(); //
            if (currentPub != null && currentPriv != null) //
            {
                File.Move(currentPub, Path.Combine(historyPath, Path.GetFileName(currentPub)), true); //
                File.Move(currentPriv, Path.Combine(historyPath, Path.GetFileName(currentPriv)), true); //
                _logger.Info($"已將舊的 RSA 金鑰對從 Current 移至 History: {Path.GetFileName(currentPub)}, {Path.GetFileName(currentPriv)}"); //
            }
            else
            {
                _logger.Info("Current 資料夾中沒有舊的 RSA 金鑰對。"); //
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"移動舊金鑰到 History 資料夾時發生錯誤，將不進行新金鑰搬移。金鑰組: {keySet.UnifiedName}"); //
            return false;
        }

        // 2. 嘗試移動新的金鑰組到 Current 資料夾
        string destAesPath = Path.Combine(currentPath, keySet.UnifiedName + ".der"); //
        string destPubPath = Path.Combine(currentPath, keySet.UnifiedName + ".public.pem"); //
        string destPrivPath = Path.Combine(currentPath, keySet.UnifiedName + ".private.pem"); //

        try
        {
            // 清理 current 資料夾中所有與當前 KeySetInfo 無關的舊金鑰
            // 這是為了在搬移新金鑰前，確保 current 資料夾是乾淨的，避免舊檔案干擾。
            // 即使之前已經移動到 history，還是做一個防禦性清理。
            foreach (var file in Directory.GetFiles(currentPath)) //
            {
                if (!file.EndsWith(".der") && !file.EndsWith(".public.pem") && !file.EndsWith(".private.pem")) continue; //

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file); //
                // 檢查是否是即將搬入的新金鑰相關檔案，如果不是則刪除
                if (Path.GetFileName(file) != Path.GetFileName(destAesPath) && //
                    Path.GetFileName(file) != Path.GetFileName(destPubPath) && //
                    Path.GetFileName(file) != Path.GetFileName(destPrivPath)) //
                {
                    try
                    {
                        File.Delete(file); //
                        _logger.Warn($"清除 Current 資料夾中殘留的舊金鑰檔案: {Path.GetFileName(file)}"); //
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"清理殘留金鑰檔案失敗: {Path.GetFileName(file)}"); //
                    }
                }
            }

            // 依序搬移所有新檔案
            File.Move(keySet.AesPath, destAesPath, true); //
            _logger.Info($"新 AES 金鑰已搬移到 Current: {Path.GetFileName(destAesPath)}"); //

            File.Move(keySet.RsaPublicKeyPath, destPubPath, true); //
            _logger.Info($"新 RSA 公鑰已搬移到 Current: {Path.GetFileName(destPubPath)}"); //

            File.Move(keySet.RsaPrivateKeyPath, destPrivPath, true); //
            _logger.Info($"新 RSA 私鑰已搬移到 Current: {Path.GetFileName(destPrivPath)}"); //

            _logger.Info($"金鑰組 {keySet.UnifiedName} 已成功搬移到 Current 資料夾。"); //
            return true;
        }
        catch (FileNotFoundException ex)
        {
            _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 時，來源檔案未找到。無法完成處理。"); //
            return false;
        }
        catch (IOException ex)
        {
            _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 到 Current 失敗 (IO 錯誤)。"); //
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 到 Current 失敗 (權限不足)。"); //
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 到 Current 時發生未預期錯誤。"); //
            return false;
        }
    }

    private bool IsAesKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.der$"); //

    private bool IsRsaPublicKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.public\\.pem$"); //

    private bool IsRsaPrivateKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.private\\.pem$"); //
}