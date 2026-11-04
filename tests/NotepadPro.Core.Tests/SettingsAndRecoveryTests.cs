using NotepadPro.Core.Models;
using NotepadPro.Core.Services;

namespace NotepadPro.Core.Tests;

public sealed class SettingsAndRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NotepadProTests", Guid.NewGuid().ToString("N"));
    private readonly SettingsService _settings;

    public SettingsAndRecoveryTests()
    {
        Directory.CreateDirectory(_root);
        _settings = new SettingsService(Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Defaults_Are_Returned_When_No_Settings_File_Exists()
    {
        var loaded = _settings.Load();
        Assert.Equal(AppThemeMode.System, loaded.Theme);
        Assert.True(loaded.ShowLineNumbers);
        Assert.Equal(4, loaded.TabSize);
    }

    [Fact]
    public void Settings_Round_Trip()
    {
        var settings = _settings.Load();
        settings.Theme = AppThemeMode.Dark;
        settings.FontSize = 19;
        settings.FontFamily = "Consolas";
        settings.ColorScheme = "one-dark";
        settings.RecentFiles.Add(@"C:\temp\a.cs");
        settings.Window.Width = 1400;
        settings.SyntaxColorOverrides["dark:keyword"] = "#AABBCC";
        Assert.True(_settings.TrySave(settings, out var error), error);

        var loaded = _settings.Load();
        Assert.Equal(AppThemeMode.Dark, loaded.Theme);
        Assert.Equal(19, loaded.FontSize);
        Assert.Equal("Consolas", loaded.FontFamily);
        Assert.Equal("one-dark", loaded.ColorScheme);
        Assert.Equal(1400, loaded.Window.Width);
        Assert.Equal("#AABBCC", loaded.SyntaxColorOverrides["dark:keyword"]);
    }

    [Fact]
    public void Out_Of_Range_Settings_Are_Clamped()
    {
        var settings = new AppSettings { FontSize = 200, TabSize = 0, AutoSaveSeconds = -2 }.Normalize();
        Assert.Equal(48, settings.FontSize);
        Assert.Equal(1, settings.TabSize);
        Assert.Equal(0, settings.AutoSaveSeconds);
    }

    [Fact]
    public void Invalid_Colour_Overrides_Are_Dropped()
    {
        var settings = new AppSettings();
        settings.SyntaxColorOverrides["dark:keyword"] = "nope";
        settings.SyntaxColorOverrides["dark:string"] = "#fff";
        settings.Normalize();
        Assert.False(settings.SyntaxColorOverrides.ContainsKey("dark:keyword"));
        Assert.True(settings.SyntaxColorOverrides.ContainsKey("dark:string"));
    }

    [Fact]
    public void Corrupt_Settings_Are_Backed_Up_And_Defaults_Returned()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settings.SettingsPath)!);
        File.WriteAllText(_settings.SettingsPath, "{ not valid json");
        var loaded = _settings.Load();
        Assert.True(loaded.ShowLineNumbers);
        Assert.NotNull(_settings.LastLoadError);
        Assert.NotEmpty(Directory.GetFiles(Path.GetDirectoryName(_settings.SettingsPath)!, "settings.json.corrupt-*"));
    }

    [Fact]
    public void Recent_Files_Are_Deduplicated_And_Limited()
    {
        var settings = new AppSettings();
        for (var i = 0; i < 20; i++) _settings.AddRecentFile(settings, $@"C:\file{i}.txt");
        _settings.AddRecentFile(settings, @"C:\file19.txt");
        Assert.Equal(15, settings.RecentFiles.Count);
        Assert.Equal(@"C:\file19.txt", settings.RecentFiles[0]);
        Assert.Equal(1, settings.RecentFiles.Count(p => p == @"C:\file19.txt"));
    }

    [Fact]
    public void Session_Round_Trip_And_Delete()
    {
        var session = new SessionState
        {
            ActiveIndex = 1,
            Documents =
            [
                new SessionDocument { FilePath = @"C:\a.txt", Title = "a.txt" },
                new SessionDocument { Title = "Untitled 2", IsDirty = true, SnapshotId = "abc" }
            ]
        };
        Assert.True(_settings.TrySaveSession(session, out var error), error);
        var loaded = _settings.LoadSession();
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.ActiveIndex);
        Assert.Equal(2, loaded.Documents.Count);
        Assert.True(loaded.Documents[1].IsDirty);
        _settings.DeleteSession();
        Assert.Null(_settings.LoadSession());
    }

    [Fact]
    public async Task Recovery_Round_Trip_And_Clear()
    {
        var recovery = new RecoveryService(Path.Combine(_root, "recovery"));
        var snapshot = new RecoverySnapshot(
            new RecoveryMetadata
            {
                Id = "doc-1",
                Title = "recovered.txt",
                IsDirty = true,
                Encoding = TextEncodingKind.Utf16LE,
                LineEnding = LineEndingKind.Unix,
                CaretOffset = 4
            },
            "unsaved content");

        Assert.True(await recovery.SaveAsync([snapshot]));
        var loaded = await recovery.LoadAsync();
        Assert.Single(loaded);
        Assert.Equal("unsaved content", loaded[0].Text);
        Assert.Equal(TextEncodingKind.Utf16LE, loaded[0].Metadata.Encoding);
        Assert.Equal(4, loaded[0].Metadata.CaretOffset);

        await recovery.ClearAsync();
        Assert.Empty(await recovery.LoadAsync());
    }

    [Fact]
    public async Task Recovery_Removes_Orphaned_Snapshots()
    {
        var recovery = new RecoveryService(Path.Combine(_root, "recovery2"));
        await recovery.SaveAsync([new RecoverySnapshot(new RecoveryMetadata { Id = "a" }, "one")]);
        await recovery.SaveAsync([new RecoverySnapshot(new RecoveryMetadata { Id = "b" }, "two")]);
        Assert.False(File.Exists(Path.Combine(recovery.DirectoryPath, "a.snapshot")));
        Assert.True(File.Exists(Path.Combine(recovery.DirectoryPath, "b.snapshot")));
    }

    [Fact]
    public void Zoom_Is_Clamped()
    {
        var settings = new AppSettings { ZoomPercent = 900 }.Normalize();
        Assert.Equal(300, settings.ZoomPercent);
    }
}
