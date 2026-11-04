namespace NotepadPro.Core.Models;

public sealed class SessionState
{
    public List<SessionDocument> Documents { get; set; } = [];
    public int ActiveIndex { get; set; }
    public WindowPlacement? Window { get; set; }
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SessionDocument
{
    public string? FilePath { get; set; }
    public string Title { get; set; } = "Untitled";
    public string? LanguageId { get; set; }
    public TextEncodingKind Encoding { get; set; } = TextEncodingKind.Utf8;
    public LineEndingKind LineEnding { get; set; } = LineEndingKind.Windows;
    public int CaretOffset { get; set; }
    public double VerticalOffset { get; set; }
    public double HorizontalOffset { get; set; }
    public bool IsDirty { get; set; }
    public string? SnapshotId { get; set; }
}

public sealed class RecoveryMetadata
{
    public required string Id { get; set; }
    public string? FilePath { get; set; }
    public string Title { get; set; } = "Untitled";
    public string? LanguageId { get; set; }
    public TextEncodingKind Encoding { get; set; } = TextEncodingKind.Utf8;
    public LineEndingKind LineEnding { get; set; } = LineEndingKind.Windows;
    public bool IsDirty { get; set; }
    public int CaretOffset { get; set; }
    public double VerticalOffset { get; set; }
    public double HorizontalOffset { get; set; }
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record RecoverySnapshot(RecoveryMetadata Metadata, string Text);
