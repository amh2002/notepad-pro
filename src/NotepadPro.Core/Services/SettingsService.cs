using System.Text.Json;
using System.Text.Json.Serialization;
using NotepadPro.Core.Models;

namespace NotepadPro.Core.Services;

/// <summary>Loads and saves preferences/sessions with atomic writes and corruption recovery.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _appDataDirectory;
    private readonly string _localDataDirectory;

    public SettingsService(string? appDataDirectory = null, string? localDataDirectory = null)
    {
        _appDataDirectory = appDataDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotepadPro");
        _localDataDirectory = localDataDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotepadPro");

        Directory.CreateDirectory(_appDataDirectory);
        Directory.CreateDirectory(_localDataDirectory);
        Directory.CreateDirectory(RecoveryDirectory);
    }

    public string SettingsPath => Path.Combine(_appDataDirectory, "settings.json");
    public string SessionPath => Path.Combine(_appDataDirectory, "session.json");
    public string RecoveryDirectory => Path.Combine(_localDataDirectory, "Recovery");
    public string ConfigDirectory => _appDataDirectory;
    public string? LastLoadError { get; private set; }

    public AppSettings Load()
    {
        LastLoadError = null;
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return (settings ?? new AppSettings()).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastLoadError = ex.Message;
            TryBackupCorruptFile(SettingsPath);
            return new AppSettings();
        }
    }

    public bool TrySave(AppSettings settings, out string? error)
    {
        error = null;
        try
        {
            settings.Normalize();
            AtomicWrite(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public SessionState? LoadSession()
    {
        try
        {
            if (!File.Exists(SessionPath)) return null;
            var json = File.ReadAllText(SessionPath);
            var session = JsonSerializer.Deserialize<SessionState>(json, JsonOptions);
            if (session is null) return null;
            session.Documents ??= [];
            session.Documents.RemoveAll(d => d is null);
            return session;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastLoadError = ex.Message;
            TryBackupCorruptFile(SessionPath);
            return null;
        }
    }

    public bool TrySaveSession(SessionState session, out string? error)
    {
        error = null;
        try
        {
            AtomicWrite(SessionPath, JsonSerializer.Serialize(session, JsonOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public void DeleteSession()
    {
        try
        {
            if (File.Exists(SessionPath)) File.Delete(SessionPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastLoadError = ex.Message;
        }
    }

    public void AddRecentFile(AppSettings settings, string path)
    {
        settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        settings.RecentFiles.Insert(0, path);
        if (settings.RecentFiles.Count > 15) settings.RecentFiles.RemoveRange(15, settings.RecentFiles.Count - 15);
    }

    public void RemoveRecentFile(AppSettings settings, string path)
        => settings.RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

    public void ClearRecentFiles(AppSettings settings) => settings.RecentFiles.Clear();

    public static string GetDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotepadPro");

    private static void AtomicWrite(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, contents, new System.Text.UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            catch
            {
                // ignored
            }
            throw;
        }
    }

    private static void TryBackupCorruptFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var backup = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(path, backup, overwrite: true);
        }
        catch
        {
            // Diagnostics must never block startup.
        }
    }
}
