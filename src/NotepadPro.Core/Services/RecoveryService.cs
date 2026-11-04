using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NotepadPro.Core.Models;

namespace NotepadPro.Core.Services;

/// <summary>
/// Crash-safe snapshots of unsaved documents. Snapshot text is stored separately from its
/// metadata so large documents can be written with ordinary UTF-8 encoding.
/// </summary>
public sealed class RecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RecoveryService(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotepadPro", "Recovery");
        Directory.CreateDirectory(_directory);
    }

    public string DirectoryPath => _directory;
    public string ManifestPath => Path.Combine(_directory, "recovery.json");

    public async Task<bool> SaveAsync(IReadOnlyList<RecoverySnapshot> snapshots, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);
            var metadata = new List<RecoveryMetadata>(snapshots.Count);
            foreach (var snapshot in snapshots)
            {
                var id = SanitizeId(snapshot.Metadata.Id);
                if (id is null) continue;

                var textPath = SnapshotPath(id);
                var temp = textPath + ".tmp";
                await File.WriteAllTextAsync(temp, snapshot.Text, new UTF8Encoding(false), cancellationToken)
                    .ConfigureAwait(false);
                File.Move(temp, textPath, overwrite: true);
                metadata.Add(snapshot.Metadata);
            }

            var manifestTemp = ManifestPath + ".tmp";
            await File.WriteAllTextAsync(manifestTemp, JsonSerializer.Serialize(metadata, JsonOptions),
                new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(manifestTemp, ManifestPath, overwrite: true);

            // Remove orphaned snapshot files.
            var known = metadata.Select(m => SanitizeId(m.Id)).Where(id => id is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(_directory, "*.snapshot"))
            {
                if (!known.Contains(Path.GetFileNameWithoutExtension(file)))
                {
                    try { File.Delete(file); } catch { /* best effort */ }
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<RecoverySnapshot>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(ManifestPath)) return [];

            var json = await File.ReadAllTextAsync(ManifestPath, cancellationToken).ConfigureAwait(false);
            var metadata = JsonSerializer.Deserialize<List<RecoveryMetadata>>(json, JsonOptions) ?? [];
            var result = new List<RecoverySnapshot>(metadata.Count);

            foreach (var entry in metadata)
            {
                var id = SanitizeId(entry.Id);
                if (id is null) continue;
                var textPath = SnapshotPath(id);
                if (!File.Exists(textPath)) continue;
                try
                {
                    var text = await File.ReadAllTextAsync(textPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    result.Add(new RecoverySnapshot(entry, text));
                }
                catch (IOException)
                {
                    // Skip an unreadable entry rather than failing all recovery.
                }
            }
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var safe = SanitizeId(id);
            if (safe is null) return;
            var path = SnapshotPath(safe);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next save will garbage-collect orphaned snapshots.
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Directory.Exists(_directory))
            {
                foreach (var file in Directory.EnumerateFiles(_directory))
                {
                    try { File.Delete(file); } catch { /* best effort */ }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ignored
        }
        finally
        {
            _gate.Release();
        }
    }

    private string SnapshotPath(string id) => Path.Combine(_directory, id + ".snapshot");

    private static string? SanitizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var safe = new string(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return safe.Length is > 0 and <= 80 ? safe : null;
    }
}
