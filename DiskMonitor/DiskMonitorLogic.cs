using System.IO;

namespace DiskMonitor;

/// <summary>
/// 지정 폴더가 위치한 드라이브의 사용량을 검사하고, 조건 충족 시 해당 폴더 트리에서 가장 오래된 파일 1개를 삭제합니다.
/// </summary>
public static class DiskMonitorLogic
{
    private static readonly EnumerationOptions FileEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = FileAttributes.System,
    };

    /// <summary>
    /// 경로가 속한 드라이브의 루트를 반환합니다. UNC 등으로 판별 불가하면 null입니다.
    /// </summary>
    public static string? GetDriveRootForPath(string folderPath)
    {
        try
        {
            var full = Path.GetFullPath(folderPath);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root))
                return null;
            return root.TrimEnd('\\');
        }
        catch
        {
            return null;
        }
    }

    public static bool TryGetDriveMetrics(string folderPath, out DriveInfo drive, out double usedPercent, out long freeBytes)
    {
        usedPercent = 0;
        freeBytes = 0;
        drive = default!;

        var root = GetDriveRootForPath(folderPath);
        if (root is null)
            return false;

        try
        {
            var di = new DriveInfo(root);
            if (!di.IsReady || di.TotalSize <= 0)
                return false;

            drive = di;
            freeBytes = di.AvailableFreeSpace;
            var used = di.TotalSize - di.AvailableFreeSpace;
            usedPercent = 100.0 * used / di.TotalSize;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool ShouldTrigger(
        string folderPath,
        ThresholdKind kind,
        double thresholdValue,
        out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            error = "폴더 경로를 입력하세요.";
            return false;
        }

        if (!Directory.Exists(folderPath))
        {
            error = "폴더가 없습니다.";
            return false;
        }

        if (!TryGetDriveMetrics(folderPath, out _, out var usedPercent, out var freeBytes))
        {
            error = "드라이브 정보를 읽을 수 없습니다. 로컬 경로인지 확인하세요.";
            return false;
        }

        return kind switch
        {
            ThresholdKind.UsedPercent => usedPercent >= thresholdValue - 1e-6,
            ThresholdKind.FreeSpaceMegabytes =>
                freeBytes < thresholdValue * 1024L * 1024L - 1,
            _ => false,
        };
    }

    /// <summary>
    /// 하위 폴더를 포함해 가장 오래된(LastWriteTimeUtc 기준) 파일 1개의 전체 경로를 반환합니다.
    /// </summary>
    public static string? FindOldestFilePath(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return null;

        string? bestPath = null;
        var bestTime = DateTime.MaxValue;

        foreach (var file in Directory.EnumerateFiles(folderPath, "*", FileEnumerationOptions))
        {
            DateTime t;
            try
            {
                t = File.GetLastWriteTimeUtc(file);
            }
            catch
            {
                continue;
            }

            if (t < bestTime)
            {
                bestTime = t;
                bestPath = file;
            }
        }

        return bestPath;
    }

    /// <summary>
    /// 파일 1개를 삭제합니다. 성공 시 삭제한 경로를 반환합니다.
    /// </summary>
    public static bool TryDeleteOldestFile(string folderPath, out string? deletedPath, out string? error)
    {
        deletedPath = null;
        error = null;

        var target = FindOldestFilePath(folderPath);
        if (target is null)
        {
            error = "삭제할 파일이 없습니다.";
            return false;
        }

        try
        {
            File.Delete(target);
            deletedPath = target;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
