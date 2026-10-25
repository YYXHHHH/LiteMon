using LiteMon.Core.Appearance;
using LiteMon.Settings;

namespace LiteMon.Settings.Tests;

public class SettingsStoreTests
{
    [Fact] // RI-10
    public void Load_MissingFile_ReturnsDefault()
    {
        string path = TempPath(out string directory);
        try
        {
            Assert.Equal(1, new SettingsStore(path).Load().RefreshIntervalSeconds);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-11
    public void SaveThenLoad_RoundTrips()
    {
        string path = TempPath(out string directory);
        try
        {
            var store = new SettingsStore(path);
            Assert.True(store.TrySave(AppSettings.Default with { RefreshIntervalSeconds = 5 }));

            var loaded = new SettingsStore(path).Load();
            Assert.Equal(5, loaded.RefreshIntervalSeconds);
            Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-12
    public void Load_CorruptJson_FallsBackAndQuarantines()
    {
        string path = TempPath(out string directory);
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{ this is not json ");

            Assert.Equal(1, new SettingsStore(path).Load().RefreshIntervalSeconds);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".bad"));
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-13
    public void Load_OutOfRange_IsClamped()
    {
        string path = TempPath(out string directory);
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"version\":1,\"refreshIntervalSeconds\":999}");

            Assert.Equal(60, new SettingsStore(path).Load().RefreshIntervalSeconds);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-14
    public void Load_UnknownFieldsAreIgnored()
    {
        string path = TempPath(out string directory);
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"version\":1,\"refreshIntervalSeconds\":2,\"theme\":\"dark\"}");

            Assert.Equal(2, new SettingsStore(path).Load().RefreshIntervalSeconds);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-15
    public void TrySave_ToMissingDrive_ReturnsFalseWithoutThrowing()
    {
        string path = Path.Combine("Z:", "definitely-missing", "settings.json");
        Assert.False(new SettingsStore(path).TrySave(AppSettings.Default));
    }

    [Fact] // RI-16
    public void Save_WritesIndentedCamelCaseJson()
    {
        string path = TempPath(out string directory);
        try
        {
            Assert.True(new SettingsStore(path).TrySave(AppSettings.Default with { RefreshIntervalSeconds = 3 }));

            string json = File.ReadAllText(path);
            Assert.Contains("\"refreshIntervalSeconds\": 3", json, StringComparison.Ordinal);
            Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"interval\"", json, StringComparison.Ordinal);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-17
    public void SaveThenLoad_KeepsPinnedStateAndPosition()
    {
        string path = TempPath(out string directory);
        try
        {
            var store = new SettingsStore(path);
            Assert.True(store.TrySave(AppSettings.Default with
            {
                RefreshIntervalSeconds = 2,
                IsPinned = true,
                WindowLeft = 1200.5,
                WindowTop = 64,
            }));

            var loaded = new SettingsStore(path).Load();
            Assert.True(loaded.IsPinned);
            Assert.Equal(1200.5, loaded.WindowLeft);
            Assert.Equal(64, loaded.WindowTop);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-18
    public void Load_OldConfigWithoutWindowFields_UsesDefaults()
    {
        string path = TempPath(out string directory);
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"version\":1,\"refreshIntervalSeconds\":5}");

            var loaded = new SettingsStore(path).Load();
            Assert.False(loaded.IsPinned);
            Assert.Null(loaded.WindowLeft);
            Assert.Null(loaded.WindowTop);
            Assert.Equal(5, loaded.RefreshIntervalSeconds);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-19
    public void SaveThenLoad_KeepsAppearance()
    {
        string path = TempPath(out string directory);
        try
        {
            var store = new SettingsStore(path);
            var appearance = new AppearanceSettings
            {
                Layout = PanelLayout.Compact,
                OpacityPercent = 75,
                ShowBars = false,
                SmoothAnimation = true,
                VisibleGroups = PanelGroup.Cpu | PanelGroup.Gpu,
            };

            Assert.True(store.TrySave(AppSettings.Default with { Appearance = appearance }));

            var loaded = new SettingsStore(path).Load();
            Assert.Equal(PanelLayout.Compact, loaded.Appearance.Layout);
            Assert.Equal(75, loaded.Appearance.OpacityPercent);
            Assert.False(loaded.Appearance.ShowBars);
            // 75% 透明会强制关闭动画（分层窗口软件渲染）
            Assert.False(loaded.Appearance.SmoothAnimation);
            Assert.Equal(PanelGroup.Cpu | PanelGroup.Gpu, loaded.Appearance.VisibleGroups);
        }
        finally
        {
            Delete(directory);
        }
    }

    [Fact] // RI-20
    public void Load_WithoutAppearance_UsesDefaultAppearance()
    {
        string path = TempPath(out string directory);
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"version\":1,\"refreshIntervalSeconds\":5}");

            var appearance = new SettingsStore(path).Load().Appearance;
            Assert.Equal(PanelLayout.Detailed, appearance.Layout);
            Assert.Equal(100, appearance.OpacityPercent);
            Assert.True(appearance.ShowBars);
            Assert.False(appearance.SmoothAnimation);
            Assert.Equal(PanelGroup.All, appearance.VisibleGroups);
        }
        finally
        {
            Delete(directory);
        }
    }

    // 注意：不用 Path.GetTempPath()——在受限沙箱里子进程可能写不了系统临时目录。
    // 放在测试程序集目录下既沙箱友好，也保证测试互不干扰。
    private static string TempPath(out string directory)
    {
        directory = Path.Combine(AppContext.BaseDirectory, "settings-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(directory, "LiteMon.settings.json");
    }

    private static void Delete(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
