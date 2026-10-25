using System.IO;
using System.Text.Json;
using LiteMon.Core.Sensors;

namespace LiteMon.Settings;

/// <summary>
/// 设置读写。只在 UI 线程使用。
/// 任何失败都不抛异常：读失败回默认值，写失败返回 false。
/// 位置：优先 exe 同目录（便携），不可写时退回 %LOCALAPPDATA%\LiteMon。
/// </summary>
public sealed class SettingsStore
{
    private const string FileName = "LiteMon.settings.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? ResolveDefaultPath() : path;
    }

    /// <summary>实际使用的配置文件路径（排障时打印它）。</summary>
    public string Path => _path;

    /// <summary>决定配置写到哪里：exe 目录可写就用它，否则用 LocalAppData。</summary>
    public static string ResolveDefaultPath()
    {
        string appDirectory = GetAppDirectory();
        string besideExe = System.IO.Path.Combine(appDirectory, FileName);

        if (File.Exists(besideExe) || IsDirectoryWritable(appDirectory))
        {
            return besideExe;
        }

        string userDirectory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiteMon");
        return System.IO.Path.Combine(userDirectory, FileName);
    }

    /// <summary>读取设置；文件缺失/损坏/越界都返回可用值。</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return AppSettings.Default;
            }

            string json = File.ReadAllText(_path);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return (settings ?? AppSettings.Default).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Quarantine();
            return AppSettings.Default;
        }
    }

    /// <summary>保存设置；失败返回 false（调用方静默处理，不影响采样与显示）。</summary>
    public bool TrySave(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            string? directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(settings.Normalized(), Options);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private static string GetAppDirectory()
    {
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            string? directory = System.IO.Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static bool IsDirectoryWritable(string directory)
    {
        try
        {
            string probe = System.IO.Path.Combine(directory, "litemon-write-probe.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>把损坏的配置改名归档，避免每次启动都解析失败。</summary>
    private void Quarantine()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Move(_path, _path + ".bad", overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // 归档失败不影响使用
        }
    }
}
