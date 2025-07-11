﻿using KeyManagerTool.CryptoLib.Models;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using NLog;

namespace KeyManagerTool.CryptoLib.Services
{
    public class KeyManagerService
    {
        private readonly string _basePath;
        private readonly string updatePath;
        private readonly string currentPath;
        private readonly string historyPath;
        private readonly object _folderProcessingLock = new();
        private readonly ILogger _logger;

        public KeyManagerService(ILogger logger, string basePath)
        {
            _logger = logger;
            _basePath = basePath;

            updatePath = Path.Combine(_basePath, "update");
            currentPath = Path.Combine(_basePath, "current");
            historyPath = Path.Combine(_basePath, "history");

            try
            {
                Directory.CreateDirectory(_basePath);
                Directory.CreateDirectory(updatePath);
                Directory.CreateDirectory(currentPath);
                Directory.CreateDirectory(historyPath);

                _logger.Info($"確保金鑰目錄結構存在：{_basePath}, {updatePath}, {currentPath}, {historyPath}");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, $"無法建立金鑰管理目錄結構。應用程式無法正常啟動：{_basePath}");

                throw;
            }

            _logger.Info("FileSystemWatcher 已移除，因為此應用程式將透過 Windows 排程單次執行。");
        }

        public Task StartAsync()
        {
            _logger.Info($"開始執行一次性金鑰處理...");

            ProcessUpdateFolderWrapper();

            _logger.Info($"一次性金鑰處理完成。");

            return Task.CompletedTask;
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
                    if (Path.GetFileNameWithoutExtension(aesFile).Split('.')[0] == group.UnifiedName &&
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

                var success = ProcessAndMoveKeySet(keySet);

                if (success)
                {
                    // 成功搬移後，從 update 資料夾中刪除原始檔案
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
                else
                {
                    _logger.Error($"金鑰組 {keySet.UnifiedName} 未能成功處理，檔案將保留在 Update 資料夾中，等待下次處理或手動介入。");
                }
            }
        }

        private bool ProcessAndMoveKeySet(KeySetInfo keySet)
        {
            _logger.Info($"嘗試移動金鑰組 {keySet.UnifiedName} 到 Current 資料夾...");

            // 1. 先處理舊金鑰：將 current 資料夾中的舊金鑰移至 history
            try
            {
                var currentAes = Directory.GetFiles(currentPath, "*.der").FirstOrDefault();

                if (currentAes != null)
                {
                    var destHistPath = Path.Combine(historyPath, Path.GetFileName(currentAes));

                    File.Move(currentAes, destHistPath, true);

                    _logger.Info($"已將舊的 AES 金鑰從 Current 移至 History: {Path.GetFileName(currentAes)}");
                }
                else
                {
                    _logger.Info("Current 資料夾中沒有舊的 AES 金鑰。");
                }

                var currentPub = Directory.GetFiles(currentPath, "*.public.pem").FirstOrDefault();
                var currentPriv = Directory.GetFiles(currentPath, "*.private.pem").FirstOrDefault();

                if (currentPub != null && currentPriv != null)
                {
                    File.Move(currentPub, Path.Combine(historyPath, Path.GetFileName(currentPub)), true);
                    File.Move(currentPriv, Path.Combine(historyPath, Path.GetFileName(currentPriv)), true);

                    _logger.Info($"已將舊的 RSA 金鑰對從 Current 移至 History: {Path.GetFileName(currentPub)}, {Path.GetFileName(currentPriv)}");
                }
                else
                {
                    _logger.Info("Current 資料夾中沒有舊的 RSA 金鑰對。");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移動舊金鑰到 History 資料夾時發生錯誤，將不進行新金鑰搬移。金鑰組: {keySet.UnifiedName}");

                return false;
            }

            var destAesPath = Path.Combine(currentPath, keySet.UnifiedName + ".der");
            var destPubPath = Path.Combine(currentPath, keySet.UnifiedName + ".public.pem");
            var destPrivPath = Path.Combine(currentPath, keySet.UnifiedName + ".private.pem");

            try
            {
                // 清理 current 資料夾中所有與當前 KeySetInfo 無關的舊金鑰
                foreach (var file in Directory.GetFiles(currentPath))
                {
                    if (!file.EndsWith(".der") && !file.EndsWith(".public.pem") && !file.EndsWith(".private.pem")) continue;

                    if (Path.GetFileName(file) != Path.GetFileName(destAesPath) &&
                        Path.GetFileName(file) != Path.GetFileName(destPubPath) &&
                        Path.GetFileName(file) != Path.GetFileName(destPrivPath))
                    {
                        try
                        {
                            File.Delete(file);

                            _logger.Warn($"清除 Current 資料夾中殘留的舊金鑰檔案: {Path.GetFileName(file)}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, $"清理殘留金鑰檔案失敗: {Path.GetFileName(file)}");
                        }
                    }
                }

                File.Move(keySet.AesPath, destAesPath, true);

                _logger.Info($"新 AES 金鑰已搬移到 Current: {Path.GetFileName(destAesPath)}");

                File.Move(keySet.RsaPublicKeyPath, destPubPath, true);

                _logger.Info($"新 RSA 公鑰已搬移到 Current: {Path.GetFileName(destPubPath)}");

                File.Move(keySet.RsaPrivateKeyPath, destPrivPath, true);

                _logger.Info($"新 RSA 私鑰已搬移到 Current: {Path.GetFileName(destPrivPath)}");

                _logger.Info($"金鑰組 {keySet.UnifiedName} 已成功搬移到 Current 資料夾。");

                return true;
            }
            catch (FileNotFoundException ex)
            {
                _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 時，來源檔案未找到。無法完成處理。");

                return false;
            }
            catch (IOException ex)
            {
                _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 到 Current 失敗 (IO 錯誤)。");

                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 到 Current 失敗 (權限不足)。");

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移動金鑰組 {keySet.UnifiedName} 到 Current 時發生未預期錯誤。");

                return false;
            }
        }

        private KeySetInfo GetKeySetInfoByUnifiedName(string unifiedName, string searchPath)
        {
            try
            {
                if (!Directory.Exists(searchPath))
                {
                    _logger.Warn($"搜索路徑 '{searchPath}' 不存在，無法查找金鑰組 '{unifiedName}'。");

                    return null;
                }

                var files = Directory.GetFiles(searchPath, $"{unifiedName}.*").ToList();

                if (files.Count == 0)
                {
                    _logger.Debug($"在 '{searchPath}' 中未找到與 unifiedName '{unifiedName}' 匹配的檔案。");

                    return null;
                }

                var aesFile = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == unifiedName && Path.GetExtension(f) == ".der");
                var rsaPubFile = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).StartsWith(unifiedName) && Path.GetFileName(f).EndsWith(".public.pem"));
                var rsaPrivFile = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).StartsWith(unifiedName) && Path.GetFileName(f).EndsWith(".private.pem"));

                if (aesFile != null && rsaPubFile != null && rsaPrivFile != null)
                {
                    var creationTime = DateTime.UtcNow;

                    try
                    {
                        creationTime = File.GetCreationTimeUtc(aesFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, $"無法獲取 AES 文件 '{aesFile}' 的創建時間，使用 UTCNow。");
                    }

                    _logger.Info($"在 '{searchPath}' 中找到 unifiedName '{unifiedName}' 的完整金鑰組。");

                    return new KeySetInfo
                    {
                        UnifiedName = unifiedName,
                        AesPath = aesFile,
                        RsaPublicKeyPath = rsaPubFile,
                        RsaPrivateKeyPath = rsaPrivFile,
                        CreationTime = creationTime
                    };
                }
                else
                {
                    _logger.Warn($"在 '{searchPath}' 中找到 unifiedName '{unifiedName}' 的部分檔案，但金鑰組不完整。");

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"獲取 unifiedName '{unifiedName}' 在 '{searchPath}' 中的金鑰組信息時發生未預期錯誤。");

                return null;
            }
        }

        public KeySetInfo GetCurrentKeySetInfo(string unifiedName)
        {
            _logger.Debug($"嘗試從 '{currentPath}' 獲取 unifiedName '{unifiedName}' 的金鑰組。");

            return GetKeySetInfoByUnifiedName(unifiedName, currentPath);
        }

        public KeySetInfo GetHistoryKeySetInfo(string unifiedName)
        {
            _logger.Debug($"嘗試從 '{historyPath}' 獲取 unifiedName '{unifiedName}' 的金鑰組。");

            return GetKeySetInfoByUnifiedName(unifiedName, historyPath);
        }

        /// <summary>
        /// 獲取當前（current）目錄中最新的活動金鑰組的 unifiedName。
        /// </summary>
        /// <returns>最新活動金鑰組的 unifiedName，如果沒有完整的金鑰組則為 null。</returns>
        public string GetLatestActiveUnifiedName()
        {
            _logger.Debug($"嘗試獲取 '{currentPath}' 中最新的活動金鑰組的 unifiedName。");

            try
            {
                var filesInCurrent = Directory.GetFiles(currentPath).ToList();

                var fileGroups = filesInCurrent
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
                        catch (Exception ex)
                        {
                            _logger.Warn(ex, $"獲取金鑰組 {group.UnifiedName} 的建立時間時發生錯誤，跳過。");
                        }
                    }
                }

                var latestKeySet = completeKeySets.OrderByDescending(ks => ks.CreationTime).FirstOrDefault();

                if (latestKeySet != null)
                {
                    _logger.Info($"在 '{currentPath}' 中找到最新的活動金鑰組: {latestKeySet.UnifiedName}");

                    return latestKeySet.UnifiedName;
                }
                else
                {
                    _logger.Warn($"在 '{currentPath}' 中未找到任何完整的金鑰組。");

                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"獲取 '{currentPath}' 中最新活動 unifiedName 時發生錯誤。");

                return null;
            }
        }

        private bool IsAesKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.der$");

        private bool IsRsaPublicKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.public\\.pem$");

        private bool IsRsaPrivateKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.private\\.pem$");
    }
}