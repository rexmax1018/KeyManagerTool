using System.Globalization;
using System.Text.RegularExpressions;

public class KeyManagerService
{
    private readonly string basePath = Path.Combine(Directory.GetCurrentDirectory(), "Keys");
    private readonly string updatePath;
    private readonly string currentPath;
    private readonly string historyPath;
    private readonly string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "keys_log.csv");
    private readonly string logDirPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");

    private int serialCounter = 1;

    private FileSystemWatcher _watcher;
    private readonly object _folderProcessingLock = new object();

    public KeyManagerService()
    {
        updatePath = Path.Combine(basePath, "update");
        currentPath = Path.Combine(basePath, "current");
        historyPath = Path.Combine(basePath, "history");

        Directory.CreateDirectory(updatePath);
        Directory.CreateDirectory(currentPath);
        Directory.CreateDirectory(historyPath);
        Directory.CreateDirectory(logDirPath);

        if (!File.Exists(logFilePath))
        {
            File.WriteAllText(logFilePath, "ID,FileName,Algorithm,KeyType,StartTime,ExpireTime,IsActive,PrevKeyID\n");
        }
        else
        {
            var lines = File.ReadAllLines(logFilePath);
            if (lines.Length > 1)
            {
                var lastLine = lines.Last();
                var lastIdStr = lastLine.Split(',')[0];
                if (int.TryParse(lastIdStr, out int lastId))
                {
                    serialCounter = lastId + 1;
                }
            }
        }

        _watcher = new FileSystemWatcher(updatePath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        _watcher.Created += OnFileCreatedOrChanged;
        _watcher.Changed += OnFileCreatedOrChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Deleted += OnFileDeleted;
    }

    public Task StartAsync()
    {
        Console.WriteLine($"[Watcher] 開始監控資料夾: {updatePath}");
        WriteLog($"開始監控資料夾: {updatePath}");

        ProcessUpdateFolder();

        _watcher.EnableRaisingEvents = true;

        return Task.CompletedTask;
    }

    private void OnFileCreatedOrChanged(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"[Watcher Event] 偵測到檔案變動: {e.FullPath}, 類型: {e.ChangeType}");
        WriteLog($"偵測到檔案變動: {e.FullPath}, 類型: {e.ChangeType}");
        ProcessUpdateFolderWrapper();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        Console.WriteLine($"[Watcher Event] 偵測到檔案更名: {e.OldFullPath} -> {e.FullPath}, 類型: {e.ChangeType}");
        WriteLog($"偵測到檔案更名: {e.OldFullPath} -> {e.FullPath}, 類型: {e.ChangeType}");
        ProcessUpdateFolderWrapper();
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        Console.WriteLine($"[Watcher Event] 偵測到檔案刪除: {e.FullPath}, 類型: {e.ChangeType}");
        WriteLog($"偵測到檔案刪除: {e.FullPath}, 類型: {e.ChangeType}");
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
        Console.WriteLine("[Watcher] 掃描 update 資料夾...");
        WriteLog("掃描 update 資料夾");

        var files = Directory.GetFiles(updatePath);

        var aesKey = Array.Find(files, f => IsAesKey(Path.GetFileName(f)));
        var rsaPublicKey = Array.Find(files, f => IsRsaPublicKey(Path.GetFileName(f)));
        var rsaPrivateKey = Array.Find(files, f => IsRsaPrivateKey(Path.GetFileName(f)));

        if (aesKey != null && rsaPublicKey != null && rsaPrivateKey != null)
        {
            string unifiedName = Path.GetFileNameWithoutExtension(aesKey);
            Console.WriteLine($"[Hybrid] 偵測到混合金鑰: {unifiedName}");
            WriteLog($"Hybrid 金鑰偵測到: {unifiedName}");
            ProcessHybridKeys(aesKey, rsaPublicKey, rsaPrivateKey, unifiedName);
        }

        foreach (var file in Directory.GetFiles(updatePath))
        {
            try { File.Delete(file); } catch { }
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

        string prevId = null;
        if (currentAes != null)
        {
            string prevFile = Path.GetFileNameWithoutExtension(currentAes);
            prevId = GetKeyIdByFileName(prevFile);

            string destHistPath = Path.Combine(historyPath, Path.GetFileName(currentAes));
            File.Move(currentAes, destHistPath, true);
            MarkKeyInactive(prevId);
            WriteLog($"AES 金鑰移至歷史區: {Path.GetFileName(currentAes)}");
        }

        string destCurrentPath = Path.Combine(currentPath, fileName + ".der");
        File.Move(aesFilePath, destCurrentPath, true);

        var now = DateTime.UtcNow;
        var expire = now.AddDays(180);

        var logLine = string.Join(",",
            serialCounter,
            fileName,
            "AES",
            "private",
            now.ToString("o", CultureInfo.InvariantCulture),
            expire.ToString("o", CultureInfo.InvariantCulture),
            "true",
            prevId ?? "null"
        );

        File.AppendAllText(logFilePath, logLine + "\n");
        WriteLog($"AES 金鑰紀錄新增: {fileName} (ID: {serialCounter})");
        serialCounter++;

        foreach (var file in Directory.GetFiles(currentPath, "*.der"))
        {
            if (Path.GetFileName(file) != Path.GetFileName(destCurrentPath))
            {
                try { File.Delete(file); WriteLog($"清除殘留 AES 金鑰: {Path.GetFileName(file)}"); } catch { }
            }
        }
    }

    private void ProcessRsaKey(string pubPath, string privPath, string overrideName = null)
    {
        string fileName = overrideName ?? Path.GetFileNameWithoutExtension(pubPath).Split('.')[0];

        string currentPub = Directory.GetFiles(currentPath, "*.public.pem").FirstOrDefault();
        string currentPriv = Directory.GetFiles(currentPath, "*.private.pem").FirstOrDefault();

        string prevPubId = null, prevPrivId = null;
        if (currentPub != null && currentPriv != null)
        {
            prevPubId = GetKeyIdByFileName(Path.GetFileNameWithoutExtension(currentPub).Split('.')[0]);
            prevPrivId = GetKeyIdByFileName(Path.GetFileNameWithoutExtension(currentPriv).Split('.')[0]);

            File.Move(currentPub, Path.Combine(historyPath, Path.GetFileName(currentPub)), true);
            File.Move(currentPriv, Path.Combine(historyPath, Path.GetFileName(currentPriv)), true);
            MarkKeyInactive(prevPubId);
            MarkKeyInactive(prevPrivId);
            WriteLog($"RSA 金鑰移至歷史區: {Path.GetFileName(currentPub)}, {Path.GetFileName(currentPriv)}");
        }

        File.Move(pubPath, Path.Combine(currentPath, fileName + ".public.pem"), true);
        File.Move(privPath, Path.Combine(currentPath, fileName + ".private.pem"), true);

        var now = DateTime.UtcNow;
        var expire = now.AddDays(180);

        var pubLog = string.Join(",",
            serialCounter,
            fileName,
            "RSA",
            "public",
            now.ToString("o", CultureInfo.InvariantCulture),
            expire.ToString("o", CultureInfo.InvariantCulture),
            "true",
            prevPubId ?? "null"
        );
        File.AppendAllText(logFilePath, pubLog + "\n");
        WriteLog($"RSA 公鑰紀錄新增: {fileName}.public.pem (ID: {serialCounter})");
        serialCounter++;

        var privLog = string.Join(",",
            serialCounter,
            fileName,
            "RSA",
            "private",
            now.ToString("o", CultureInfo.InvariantCulture),
            expire.ToString("o", CultureInfo.InvariantCulture),
            "true",
            prevPrivId ?? "null"
        );
        File.AppendAllText(logFilePath, privLog + "\n");
        WriteLog($"RSA 私鑰紀錄新增: {fileName}.private.pem (ID: {serialCounter})");
        serialCounter++;

        foreach (var file in Directory.GetFiles(currentPath, "*.public.pem").Concat(Directory.GetFiles(currentPath, "*.private.pem")))
        {
            if (!file.Contains(fileName))
            {
                try { File.Delete(file); WriteLog($"清除殘留 RSA 金鑰: {Path.GetFileName(file)}"); } catch { }
            }
        }
    }

    private void MarkKeyInactive(string id)
    {
        if (id == null) return;

        var lines = File.ReadAllLines(logFilePath).ToList();
        for (int i = 1; i < lines.Count; i++)
        {
            var parts = lines[i].Split(',');
            if (parts[0] == id)
            {
                parts[6] = "false";
                lines[i] = string.Join(",", parts);
                WriteLog($"金鑰標記為停用: ID {id}");
                break;
            }
        }
        File.WriteAllLines(logFilePath, lines);
    }

    private string GetKeyIdByFileName(string fileName)
    {
        var lines = File.ReadAllLines(logFilePath).Reverse();
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length >= 2 && parts[1] == fileName)
                return parts[0];
        }
        return null;
    }

    private bool IsAesKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.der$");

    private bool IsRsaPublicKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.public\\.pem$");

    private bool IsRsaPrivateKey(string fileName) => Regex.IsMatch(fileName, "^[a-zA-Z0-9]{8}\\.private\\.pem$");

    private void WriteLog(string message)
    {
        string logPath = Path.Combine(logDirPath, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
        File.AppendAllText(logPath, $"[{DateTime.UtcNow:HH:mm:ss}] {message}\n");
    }
}