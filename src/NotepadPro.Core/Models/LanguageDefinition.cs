namespace NotepadPro.Core.Models;

/// <summary>
/// Declarative description of a language. The UI layer turns these definitions into
/// AvalonEdit highlighting rules, so keyword/comment editing stays in one place.
/// </summary>
public sealed class LanguageDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string[] Extensions { get; init; } = [];
    public string[] FileNames { get; init; } = [];
    public string? LineComment { get; init; }
    public string? BlockCommentStart { get; init; }
    public string? BlockCommentEnd { get; init; }
    public string[] Keywords { get; init; } = [];
    public string[] TypeKeywords { get; init; } = [];
    public string[] Constants { get; init; } = [];
    public string[] Builtins { get; init; } = [];
    public string[] Properties { get; init; } = [];
    public string[] Tags { get; init; } = [];
    public string[] Attributes { get; init; } = [];
    public bool CaseInsensitive { get; init; }
    public bool FoldByIndentation { get; init; }
    public bool AutoIndent { get; init; } = true;

    public bool SupportsFolding => FoldByIndentation || BlockCommentStart is not null ||
        Id is "csharp" or "c" or "cpp" or "java" or "javascript" or "typescript" or "go" or "rust" or "php" or "json" or "scss" or "css" or "html" or "xml";

    public bool IsPlainText => Id == "plaintext";

    public override string ToString() => DisplayName;
}
