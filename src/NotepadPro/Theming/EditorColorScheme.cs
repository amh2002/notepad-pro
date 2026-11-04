using System.Windows.Media;

namespace NotepadPro.Theming;

/// <summary>A syntax/editor colour scheme. Colours are stored as hex strings so they can be serialised.</summary>
public sealed class EditorColorScheme
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required bool IsDark { get; init; }

    public required string Background { get; init; }
    public required string Foreground { get; init; }
    public required string Gutter { get; init; }
    public required string LineNumber { get; init; }
    public required string CurrentLine { get; init; }
    public required string CurrentLineBorder { get; init; }
    public required string Selection { get; init; }
    public required string SelectionBorder { get; init; }
    public required string IndentGuide { get; init; }
    public required string Bracket { get; init; }
    public required string BracketBackground { get; init; }
    public required string SearchMatch { get; init; }
    public required string SearchCurrent { get; init; }
    public required string Caret { get; init; }
    public required string PanelBackground { get; init; }
    public required string PanelBorder { get; init; }

    public required Dictionary<string, string> Syntax { get; init; }

    public EditorColorScheme WithSyntax(Dictionary<string, string> syntax) => new()
    {
        Id = Id,
        DisplayName = DisplayName,
        Description = Description,
        IsDark = IsDark,
        Background = Background,
        Foreground = Foreground,
        Gutter = Gutter,
        LineNumber = LineNumber,
        CurrentLine = CurrentLine,
        CurrentLineBorder = CurrentLineBorder,
        Selection = Selection,
        SelectionBorder = SelectionBorder,
        IndentGuide = IndentGuide,
        Bracket = Bracket,
        BracketBackground = BracketBackground,
        SearchMatch = SearchMatch,
        SearchCurrent = SearchCurrent,
        Caret = Caret,
        PanelBackground = PanelBackground,
        PanelBorder = PanelBorder,
        Syntax = syntax
    };
}

public static class SyntaxCategories
{
    public sealed record Category(string Key, string DisplayName, string Group);

    public static IReadOnlyList<Category> All { get; } =
    [
        new("comment", "Comments", "Core"),
        new("keyword", "Keywords", "Core"),
        new("string", "Strings", "Core"),
        new("number", "Numbers", "Core"),
        new("type", "Types", "Core"),
        new("class", "Classes", "Core"),
        new("function", "Functions", "Core"),
        new("variable", "Variables", "Core"),
        new("operator", "Operators", "Core"),
        new("constant", "Constants", "Core"),
        new("builtin", "Built-in functions", "Core"),
        new("property", "Properties", "Markup"),
        new("tag", "HTML / XML tags", "Markup"),
        new("attribute", "Attributes", "Markup"),
        new("preprocessor", "Preprocessor / decorators", "Advanced"),
        new("regex", "Regular expressions", "Advanced"),
        new("escape", "Escape sequences", "Advanced"),
        new("todo", "TODO / FIXME", "Advanced"),
        new("heading", "Markdown headings", "Markdown"),
        new("link", "Links", "Markdown"),
        new("code", "Inline code", "Markdown"),
        new("emphasis", "Emphasis", "Markdown"),
        new("quote", "Block quotes", "Markdown")
    ];

    public static string DisplayName(string key) =>
        All.FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? key;

    public static string ToBrushHex(string color)
    {
        try
        {
            var parsed = (Color)ColorConverter.ConvertFromString(color)!;
            return $"#{parsed.R:X2}{parsed.G:X2}{parsed.B:X2}";
        }
        catch
        {
            return "#FFFFFF";
        }
    }
}
