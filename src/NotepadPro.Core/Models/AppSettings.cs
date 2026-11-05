using NotepadPro.Core.Models;

namespace NotepadPro.Core.Models;

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

public sealed class WindowPlacement
{
    public double Width { get; set; } = 1180;
    public double Height { get; set; } = 780;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Maximized { get; set; }
}

/// <summary>All persisted user preferences. Kept plain so it can be JSON serialised safely.</summary>
public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // Appearance
    public AppThemeMode Theme { get; set; } = AppThemeMode.System;
    public string ColorScheme { get; set; } = "auto";
    public string FontFamily { get; set; } = "Cascadia Mono";
    public double FontSize { get; set; } = 14;
    public bool ShowLineNumbers { get; set; } = true;
    public bool HighlightCurrentLine { get; set; } = true;
    public bool ShowIndentationGuides { get; set; } = true;
    public bool MatchBrackets { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowWhitespace { get; set; }
    public bool ShowEndOfLine { get; set; }

    // Editor behaviour
    public int TabSize { get; set; } = 4;
    public bool ConvertTabsToSpaces { get; set; }
    public bool WordWrap { get; set; }
    public bool EnableFolding { get; set; } = true;
    public bool AutoIndent { get; set; } = true;
    public bool AutoDetectLanguageOnPaste { get; set; } = true;
    public bool TrimTrailingWhitespaceOnSave { get; set; }
    public bool EnableTextDragDrop { get; set; } = true;
    public double ZoomPercent { get; set; } = 100;

    // Files
    public TextEncodingKind DefaultEncoding { get; set; } = TextEncodingKind.Utf8;
    public LineEndingKind DefaultLineEnding { get; set; } = LineEndingKind.Windows;
    public bool RestoreSession { get; set; } = true;
    public bool AutoSaveRecovery { get; set; } = true;
    public int AutoSaveSeconds { get; set; } = 20;
    public List<string> RecentFiles { get; set; } = [];

    // Window
    public WindowPlacement Window { get; set; } = new();

    // Syntax colour overrides, keyed "scheme:category" -> #RRGGBB.
    public Dictionary<string, string> SyntaxColorOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public AppSettings Clone()
    {
        var clone = (AppSettings)MemberwiseClone();
        clone.RecentFiles = [.. RecentFiles];
        clone.SyntaxColorOverrides = new Dictionary<string, string>(SyntaxColorOverrides, StringComparer.OrdinalIgnoreCase);
        clone.Window = new WindowPlacement
        {
            Width = Window.Width,
            Height = Window.Height,
            Left = Window.Left,
            Top = Window.Top,
            Maximized = Window.Maximized
        };
        return clone;
    }

    /// <summary>Repairs out-of-range or malformed values after loading user configuration.</summary>
    public AppSettings Normalize()
    {
        if (Version <= 0) Version = 1;
        if (string.IsNullOrWhiteSpace(ColorScheme)) ColorScheme = "auto";
        if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Cascadia Mono";
        FontSize = Math.Clamp(FontSize, 8, 48);
        TabSize = Math.Clamp(TabSize, 1, 8);
        AutoSaveSeconds = Math.Clamp(AutoSaveSeconds, 0, 600);
        ZoomPercent = Math.Clamp(ZoomPercent, 50, 300);
        Window ??= new WindowPlacement();
        Window.Width = Math.Clamp(Window.Width, 420, 10000);
        Window.Height = Math.Clamp(Window.Height, 300, 10000);
        if (Window.Left is < -20000 or > 20000) Window.Left = null;
        if (Window.Top is < -20000 or > 20000) Window.Top = null;

        RecentFiles ??= [];
        RecentFiles = RecentFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();

        SyntaxColorOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in SyntaxColorOverrides.Keys.ToList())
        {
            var value = SyntaxColorOverrides[key];
            if (!IsValidHexColor(value)) SyntaxColorOverrides.Remove(key);
            else SyntaxColorOverrides[key] = value.ToUpperInvariant();
        }

        return this;
    }

    public static bool IsValidHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (text.Length is not (4 or 7 or 9)) return false;
        for (var i = 1; i < text.Length; i++)
        {
            if (!Uri.IsHexDigit(text[i])) return false;
        }
        return true;
    }
}
