namespace VigilWatt.Core.Utils;

/// <summary>
/// 功 能： 统一管理应用数据根目录。
/// 2026/10/06 15:53:36 xliu
/// 取值优先级从高到低：
/// 1. 环境变量 <see cref="DataDirVariable"/>——显式指定，调试/隔离时用；
/// 2. 安装/免安装根目录下的 <c>data\</c>——Debug 与 Release 一致，便携、可随目录整体搬走；
/// 3. 安装版由 Velopack 的 <c>current\</c> 回到上一级（<c>Update.exe</c> 所在目录），
///    避免升级换 <c>current</c> 时把设置/历史清掉。
/// 历史版本把 Release 数据放在 <c>%LOCALAPPDATA%\VigilWatt</c>，首次启动会迁移一次。
/// </summary>
public static class AppPaths
{
    public const string AppName = "VigilWatt";

    /// <summary>覆盖数据根目录的环境变量。优先级最高，用于临时把数据指到别处。</summary>
    public const string DataDirVariable = "VIGILWATT_DATA_DIR";

    /// <summary>安装/免安装根目录：exe 在 <c>current\</c> 下时取它的上一级，否则就是 exe 目录。</summary>
    public static readonly string AppRootDir = ResolveAppRootDir();

    /// <summary>当前是否运行在 Velopack 的 <c>current\</c> 布局里（安装版或便携版）。</summary>
    public static readonly bool IsPackagedLayout = !PathsEqual(AppRootDir, AppContext.BaseDirectory);

    public static readonly string RootDir = ResolveRootDir();

    /// <summary>旧版 Release 的数据目录，仅用于首次迁移。</summary>
    public static readonly string LegacyRootDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    private static string ResolveAppRootDir()
    {
        var baseDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var parent = Directory.GetParent(baseDir)?.FullName;
        if (parent is not null
            && string.Equals(Path.GetFileName(baseDir), "current", StringComparison.OrdinalIgnoreCase)
            && File.Exists(Path.Combine(parent, "Update.exe")))
        {
            return parent;
        }

        return baseDir;
    }

    private static string ResolveRootDir()
    {
        var custom = Environment.GetEnvironmentVariable(DataDirVariable);
        if (!string.IsNullOrWhiteSpace(custom))
        {
            try { return Path.GetFullPath(custom.Trim()); }
            catch (Exception) { /* 退回下一级 */ }
        }

        return Path.Combine(AppRootDir, "data");
    }

    /// <summary>
    /// 把旧版 <c>%LOCALAPPDATA%\VigilWatt</c> 的 settings/history/logs 搬到新的根目录。
    /// 只在打包布局、且新目录还没有 settings.json 时做一次；复制失败不影响启动。
    /// </summary>
    public static bool MigrateLegacyDataIfNeeded()
    {
        if (!IsPackagedLayout) return false;

        try
        {
            var newSettings = Path.Combine(RootDir, "settings.json");
            var oldSettings = Path.Combine(LegacyRootDir, "settings.json");
            if (PathsEqual(RootDir, LegacyRootDir) || File.Exists(newSettings) || !File.Exists(oldSettings))
                return false;

            Directory.CreateDirectory(RootDir);
            CopyIfExists(Path.Combine(LegacyRootDir, "history.db"), Path.Combine(RootDir, "history.db"));
            CopyIfExists(Path.Combine(LegacyRootDir, "history.db-wal"), Path.Combine(RootDir, "history.db-wal"));
            CopyIfExists(Path.Combine(LegacyRootDir, "history.db-shm"), Path.Combine(RootDir, "history.db-shm"));

            var oldLogs = Path.Combine(LegacyRootDir, "logs");
            if (Directory.Exists(oldLogs))
            {
                var newLogs = Path.Combine(RootDir, "logs");
                Directory.CreateDirectory(newLogs);
                foreach (var file in Directory.EnumerateFiles(oldLogs, "app-*.log"))
                    CopyIfExists(file, Path.Combine(newLogs, Path.GetFileName(file)));
                CopyIfExists(Path.Combine(oldLogs, "log-error.txt"), Path.Combine(newLogs, "log-error.txt"));
            }

            // settings 最后复制：它存在即代表迁移完成，下次不再重复。
            File.Copy(oldSettings, newSettings, overwrite: false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (File.Exists(source) && !File.Exists(destination))
            File.Copy(source, destination, overwrite: false);
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
}
