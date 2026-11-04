using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using NotepadPro.Core.Models;

namespace NotepadPro.Theming;

public sealed class ThemeChangedEventArgs(bool isDark, EditorColorScheme scheme) : EventArgs
{
    public bool IsDark { get; } = isDark;
    public EditorColorScheme Scheme { get; } = scheme;
}

/// <summary>
/// Owns the application palette and editor colour scheme. UI brushes live in
/// Themes/Light.xaml and Themes/Dark.xaml; editor/syntax brushes are projected into
/// application resources so the editor and panels can bind to them dynamically.
/// </summary>
public sealed class ThemeService
{
    private readonly Application _application;
    private ResourceDictionary? _themeDictionary;
    private bool? _loadedDark;
    private EditorColorScheme? _scheme;

    public ThemeService(Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public bool IsDark { get; private set; }
    public EditorColorScheme Scheme => _scheme ??= BuildScheme(ColorSchemes.GetById("vs-dark"), new Dictionary<string, string>());
    public int Version { get; private set; }
    public event EventHandler<ThemeChangedEventArgs>? Changed;

    public void Apply(AppSettings settings)
    {
        var isDark = settings.Theme switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => IsSystemDarkTheme()
        };

        var shellChanged = _loadedDark != isDark;
        if (shellChanged)
        {
            LoadShellDictionary(isDark);
            _loadedDark = isDark;
        }

        var baseScheme = ColorSchemes.Resolve(settings.ColorScheme, isDark);
        _scheme = BuildScheme(baseScheme, settings.SyntaxColorOverrides);
        IsDark = isDark;
        ApplyEditorBrushes(_scheme);
        Version++;

        if (shellChanged || _scheme.Id != baseScheme.Id)
        {
            Changed?.Invoke(this, new ThemeChangedEventArgs(isDark, _scheme));
        }
        else
        {
            // Colour overrides can change without a shell reload.
            Changed?.Invoke(this, new ThemeChangedEventArgs(isDark, _scheme));
        }
    }

    public static string GetEffectiveSchemeId(AppSettings settings, bool? forceDark = null)
    {
        var dark = forceDark ?? (settings.Theme switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => IsSystemDarkTheme()
        });
        return ColorSchemes.ResolveId(settings.ColorScheme, dark);
    }

    public static bool IsSystemDarkTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return false;
        }
    }

    private static EditorColorScheme BuildScheme(EditorColorScheme baseScheme,
        IReadOnlyDictionary<string, string> overrides)
    {
        var syntax = new Dictionary<string, string>(baseScheme.Syntax, StringComparer.OrdinalIgnoreCase);
        if (overrides.Count > 0)
        {
            var prefix = baseScheme.Id + ":";
            foreach (var pair in overrides)
            {
                if (!pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                var category = pair.Key[prefix.Length..];
                if (!syntax.ContainsKey(category)) continue;
                if (AppSettings.IsValidHexColor(pair.Value)) syntax[category] = NormalizeHex(pair.Value);
            }
        }
        return baseScheme.WithSyntax(syntax);
    }

    private static string NormalizeHex(string value)
    {
        var text = value.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (text.Length == 4)
        {
            text = $"#{text[1]}{text[1]}{text[2]}{text[2]}{text[3]}{text[3]}";
        }
        return text.ToUpperInvariant();
    }

    private void LoadShellDictionary(bool dark)
    {
        var source = new Uri(
            $"pack://application:,,,/NotepadPro;component/Themes/{(dark ? "Dark" : "Light")}.xaml",
            UriKind.Absolute);
        var dictionary = new ResourceDictionary { Source = source };
        var merged = _application.Resources.MergedDictionaries;
        if (_themeDictionary is null)
        {
            // App.xaml ships a default dictionary for design-time support. Reuse its slot so
            // switching to light mode does not leave the dark palette layered on top.
            _themeDictionary = merged.FirstOrDefault(dictionarySource =>
                dictionarySource.Source is not null &&
                (dictionarySource.Source.OriginalString.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                 dictionarySource.Source.OriginalString.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)));
        }

        if (_themeDictionary is not null)
        {
            var index = merged.IndexOf(_themeDictionary);
            if (index >= 0) merged[index] = dictionary;
            else merged.Insert(0, dictionary);
        }
        else
        {
            merged.Insert(0, dictionary);
        }
        _themeDictionary = dictionary;
    }

    private void ApplyEditorBrushes(EditorColorScheme scheme)
    {
        SetBrush("Editor.BackgroundBrush", scheme.Background);
        SetBrush("Editor.ForegroundBrush", scheme.Foreground);
        SetBrush("Editor.GutterBrush", scheme.Gutter);
        SetBrush("Editor.LineNumberBrush", scheme.LineNumber);
        SetBrush("Editor.CurrentLineBrush", scheme.CurrentLine);
        SetBrush("Editor.CurrentLineBorderBrush", scheme.CurrentLineBorder);
        SetBrush("Editor.SelectionBrush", scheme.Selection);
        SetBrush("Editor.SelectionBorderBrush", scheme.SelectionBorder);
        SetBrush("Editor.IndentGuideBrush", scheme.IndentGuide);
        SetBrush("Editor.BracketBrush", scheme.Bracket);
        SetBrush("Editor.BracketBackgroundBrush", scheme.BracketBackground);
        SetBrush("Editor.SearchMatchBrush", scheme.SearchMatch);
        SetBrush("Editor.SearchCurrentBrush", scheme.SearchCurrent);
        SetBrush("Editor.CaretBrush", scheme.Caret);
        SetBrush("Editor.PanelBackgroundBrush", scheme.PanelBackground);
        SetBrush("Editor.PanelBorderBrush", scheme.PanelBorder);
    }

    private void SetBrush(string key, string hex)
    {
        var brush = new SolidColorBrush(ParseColor(hex));
        brush.Freeze();
        _application.Resources[key] = brush;
    }

    public static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex)!;
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush(ParseColor(hex));
        brush.Freeze();
        return brush;
    }
}
