using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;
using NotepadPro.Theming;

namespace NotepadPro.Dialogs;

public partial class SettingsWindow : DialogWindow
{
    private sealed record SchemeChoice(string Id, string DisplayName);
    private sealed record EncodingChoice(TextEncodingKind Kind, string Name);
    private sealed record LineEndingChoice(LineEndingKind Kind, string Name);
    private sealed record SyntaxRow(string Key, string BaseColor, TextBox Box, Button Swatch);

    private readonly SettingsService _settingsService;
    private readonly List<SyntaxRow> _syntaxRows = [];
    private AppSettings _working;
    private bool _loading;

    public SettingsWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _working = settings.Clone();

        SchemeBox.ItemsSource = new[] { new SchemeChoice(ColorSchemes.AutoId, "Auto (match theme)") }
            .Concat(ColorSchemes.All.Select(scheme => new SchemeChoice(scheme.Id, scheme.DisplayName)))
            .ToList();
        EncodingBox.ItemsSource = TextEncodingKinds.All
            .Select(kind => new EncodingChoice(kind, kind.ToDisplayName()))
            .ToList();
        LineEndingBox.ItemsSource = LineEndingKinds.All
            .Select(kind => new LineEndingChoice(kind, kind.ToDisplayName()))
            .ToList();
        TabSizeBox.ItemsSource = Enumerable.Range(1, 8).ToList();

        _loading = true;
        LoadFromSettings();
        _loading = false;
        BuildSyntaxRows();
    }

    public AppSettings? ResultSettings { get; private set; }

    private void LoadFromSettings()
    {
        ThemeModeBox.SelectedIndex = (int)_working.Theme;
        SchemeBox.SelectedItem = ((IEnumerable<SchemeChoice>)SchemeBox.ItemsSource)
            .FirstOrDefault(choice => choice.Id.Equals(_working.ColorScheme, StringComparison.OrdinalIgnoreCase)) ??
            ((IEnumerable<SchemeChoice>)SchemeBox.ItemsSource).First();

        FontFamilyBox.Text = _working.FontFamily;
        FontSizeSlider.Value = Math.Clamp(_working.FontSize, FontSizeSlider.Minimum, FontSizeSlider.Maximum);
        FontSizeText.Text = $"{Math.Round(_working.FontSize)} px";
        LineNumbersCheck.IsChecked = _working.ShowLineNumbers;
        CurrentLineCheck.IsChecked = _working.HighlightCurrentLine;
        IndentGuidesCheck.IsChecked = _working.ShowIndentationGuides;
        MatchBracketsCheck.IsChecked = _working.MatchBrackets;
        StatusBarCheck.IsChecked = _working.ShowStatusBar;
        WhitespaceCheck.IsChecked = _working.ShowWhitespace;
        EndOfLineCheck.IsChecked = _working.ShowEndOfLine;

        TabSizeBox.SelectedItem = Math.Clamp(_working.TabSize, 1, 8);
        AutoSaveSecondsBox.Text = _working.AutoSaveSeconds.ToString(CultureInfo.CurrentCulture);
        ConvertTabsCheck.IsChecked = _working.ConvertTabsToSpaces;
        WordWrapCheck.IsChecked = _working.WordWrap;
        FoldingCheck.IsChecked = _working.EnableFolding;
        AutoIndentCheck.IsChecked = _working.AutoIndent;
        TrimOnSaveCheck.IsChecked = _working.TrimTrailingWhitespaceOnSave;
        DragDropCheck.IsChecked = _working.EnableTextDragDrop;
        RestoreSessionCheck.IsChecked = _working.RestoreSession;
        AutoRecoveryCheck.IsChecked = _working.AutoSaveRecovery;

        EncodingBox.SelectedItem = ((IEnumerable<EncodingChoice>)EncodingBox.ItemsSource)
            .FirstOrDefault(choice => choice.Kind == _working.DefaultEncoding);
        LineEndingBox.SelectedItem = ((IEnumerable<LineEndingChoice>)LineEndingBox.ItemsSource)
            .FirstOrDefault(choice => choice.Kind == _working.DefaultLineEnding);

        RecentSummaryText.Text = _working.RecentFiles.Count == 0
            ? "No recent files yet."
            : $"{_working.RecentFiles.Count} recent file{(_working.RecentFiles.Count == 1 ? string.Empty : "s")} remembered.";
        ClearRecentButton.IsEnabled = _working.RecentFiles.Count > 0;

        SettingsPathText.Text = _settingsService.ConfigDirectory;
        RecoveryPathText.Text = _settingsService.RecoveryDirectory;
        AdvancedConfigText.Text = Path.Combine(_settingsService.ConfigDirectory, "settings.json");

        UpdateFontPreview();
        UpdateSyntaxSchemeText();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppearanceSection is null) return;
        var tag = (NavList.SelectedItem as ListBoxItem)?.Tag as string ?? "appearance";
        AppearanceSection.Visibility = tag == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        EditorSection.Visibility = tag == "editor" ? Visibility.Visible : Visibility.Collapsed;
        FilesSection.Visibility = tag == "files" ? Visibility.Visible : Visibility.Collapsed;
        ColorsSection.Visibility = tag == "colors" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedSection.Visibility = tag == "advanced" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ThemeModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        UpdateSyntaxSchemeText();
        if (_working.ColorScheme.Equals(ColorSchemes.AutoId, StringComparison.OrdinalIgnoreCase))
            BuildSyntaxRows();
    }

    private void SchemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        UpdateSyntaxSchemeText();
        BuildSyntaxRows();
    }

    private void FontFamilyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        UpdateFontPreview();
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeText is null) return;
        FontSizeText.Text = $"{Math.Round(e.NewValue)} px";
        UpdateFontPreview();
    }

    private void UpdateFontPreview()
    {
        if (FontPreview is null) return;
        var name = string.IsNullOrWhiteSpace(FontFamilyBox.Text) ? "Cascadia Mono" : FontFamilyBox.Text.Trim();
        var size = Math.Round(FontSizeSlider.Value);
        try
        {
            FontPreview.FontFamily = new FontFamily(name);
        }
        catch
        {
            FontPreview.FontFamily = new FontFamily("Segoe UI");
        }
        FontPreview.FontSize = Math.Min(size, 26);
        FontPreviewMeta.Text = $"{name} · {size} px";
    }

    private (EditorColorScheme Scheme, bool Dark) GetEffectiveScheme()
    {
        var theme = (AppThemeMode)Math.Max(0, ThemeModeBox.SelectedIndex);
        var dark = theme switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => ThemeService.IsSystemDarkTheme()
        };
        var choice = SchemeBox.SelectedItem as SchemeChoice;
        return (ColorSchemes.Resolve(choice?.Id, dark), dark);
    }

    private void UpdateSyntaxSchemeText()
    {
        if (SyntaxSchemeText is null) return;
        var (scheme, _) = GetEffectiveScheme();
        SyntaxSchemeText.Text =
            $"Editing the {scheme.DisplayName} palette. Overrides apply to this scheme only; other schemes keep their own colors.";
    }

    private void BuildSyntaxRows()
    {
        if (SyntaxPanel is null) return;
        SyntaxPanel.Children.Clear();
        _syntaxRows.Clear();

        var (scheme, _) = GetEffectiveScheme();
        UpdateSyntaxSchemeText();

        foreach (var category in SyntaxCategories.All)
        {
            var key = $"{scheme.Id}:{category.Key}";
            var baseColor = scheme.Syntax.TryGetValue(category.Key, out var baseValue) ? baseValue : scheme.Foreground;
            var color = _working.SyntaxColorOverrides.TryGetValue(key, out var overrideValue) &&
                        AppSettings.IsValidHexColor(overrideValue)
                ? NormalizeHex(overrideValue)
                : baseColor;

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 7) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });

            var swatch = new Button
            {
                Style = (Style)FindResource("ColorSwatchButton"),
                Background = ThemeService.Brush(color),
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Choose a color"
            };
            AutomationProperties.SetName(swatch, $"Choose {category.DisplayName} color");
            AutomationProperties.SetAutomationId(swatch, $"Swatch_{category.Key}");

            var name = new TextBlock
            {
                Text = category.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("App.TextPrimaryBrush")
            };

            var box = new TextBox
            {
                Text = color,
                Width = 104,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Tag = key
            };

            var row = new SyntaxRow(key, baseColor, box, swatch);
            _syntaxRows.Add(row);

            swatch.Click += (_, _) => ChooseColor(row);
            box.TextChanged += (_, _) => OnSyntaxHexChanged(row);
            box.LostFocus += (_, _) => CommitSyntaxHex(row);

            Grid.SetColumn(swatch, 0);
            Grid.SetColumn(name, 1);
            Grid.SetColumn(box, 2);
            grid.Children.Add(swatch);
            grid.Children.Add(name);
            grid.Children.Add(box);
            SyntaxPanel.Children.Add(grid);
        }
    }

    private void ChooseColor(SyntaxRow row)
    {
        var picker = new ColorPickerWindow(row.Box.Text) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            row.Box.Text = picker.SelectedHex;
            CommitSyntaxHex(row);
        }
    }

    private void OnSyntaxHexChanged(SyntaxRow row)
    {
        if (_loading) return;
        var text = row.Box.Text.Trim();
        if (text.Length == 0)
        {
            row.Box.BorderBrush = (Brush)FindResource("App.InputBorderBrush");
            return;
        }
        if (!AppSettings.IsValidHexColor(text))
        {
            row.Box.BorderBrush = (Brush)FindResource("App.DangerBrush");
            return;
        }

        row.Box.BorderBrush = (Brush)FindResource("App.InputBorderBrush");
        var normalized = NormalizeHex(text);
        _working.SyntaxColorOverrides[row.Key] = normalized;
        row.Swatch.Background = ThemeService.Brush(normalized);
    }

    private void CommitSyntaxHex(SyntaxRow row)
    {
        var text = row.Box.Text.Trim();
        if (text.Length == 0)
        {
            _working.SyntaxColorOverrides.Remove(row.Key);
            row.Box.Text = row.BaseColor;
            row.Swatch.Background = ThemeService.Brush(row.BaseColor);
            return;
        }
        if (!AppSettings.IsValidHexColor(text))
        {
            var existing = _working.SyntaxColorOverrides.TryGetValue(row.Key, out var value) ? value : row.BaseColor;
            row.Box.Text = existing;
            row.Box.BorderBrush = (Brush)FindResource("App.InputBorderBrush");
            return;
        }

        var normalized = NormalizeHex(text);
        row.Box.Text = normalized;
        _working.SyntaxColorOverrides[row.Key] = normalized;
        row.Swatch.Background = ThemeService.Brush(normalized);
    }

    private void ResetSyntax_Click(object sender, RoutedEventArgs e)
    {
        var (scheme, _) = GetEffectiveScheme();
        var prefix = scheme.Id + ":";
        foreach (var key in _working.SyntaxColorOverrides.Keys
                     .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            _working.SyntaxColorOverrides.Remove(key);
        }
        BuildSyntaxRows();
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.ClearRecentFiles(_working);
        RecentSummaryText.Text = "No recent files yet.";
        ClearRecentButton.IsEnabled = false;
    }

    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
        => OpenFolder(_settingsService.ConfigDirectory);

    private void OpenRecoveryFolder_Click(object sender, RoutedEventArgs e)
        => OpenFolder(_settingsService.RecoveryDirectory);

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageWindow.Inform(this, "Notepad Pro", "The folder could not be opened.", ex.Message, warning: true);
        }
    }

    private void ResetWindow_Click(object sender, RoutedEventArgs e)
    {
        _working.Window = new WindowPlacement();
        MessageWindow.Inform(this, "Notepad Pro", "Window placement will be reset.",
            "The window size, position, and maximized state return to their defaults after you choose OK.");
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var reset = MessageWindow.Confirm(
            this,
            "Reset settings",
            "Reset every preference to its default value?",
            "Theme, font, editor behaviour, and syntax color overrides will return to their defaults. This cannot be undone.",
            "Reset settings",
            warning: true);
        if (!reset) return;

        _working = new AppSettings();
        _loading = true;
        LoadFromSettings();
        _loading = false;
        BuildSyntaxRows();
    }

    private bool GatherSettings(out string? error)
    {
        error = null;
        if (ThemeModeBox.SelectedIndex < 0 || SchemeBox.SelectedItem is not SchemeChoice scheme)
        {
            error = "Choose a theme and color scheme.";
            return false;
        }

        var fontFamily = FontFamilyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(fontFamily)) fontFamily = "Cascadia Mono";

        if (!int.TryParse(AutoSaveSecondsBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture,
                out var autoSaveSeconds) || autoSaveSeconds is < 0 or > 600)
        {
            error = "Enter an autosave interval from 0 to 600 seconds (0 disables the timer).";
            return false;
        }

        if (TabSizeBox.SelectedItem is not int tabSize) tabSize = 4;
        if (EncodingBox.SelectedItem is not EncodingChoice encoding ||
            LineEndingBox.SelectedItem is not LineEndingChoice lineEnding)
        {
            error = "Choose a default encoding and line ending.";
            return false;
        }

        _working.Theme = (AppThemeMode)ThemeModeBox.SelectedIndex;
        _working.ColorScheme = scheme.Id;
        _working.FontFamily = fontFamily;
        _working.FontSize = Math.Round(FontSizeSlider.Value);
        _working.ShowLineNumbers = LineNumbersCheck.IsChecked == true;
        _working.HighlightCurrentLine = CurrentLineCheck.IsChecked == true;
        _working.ShowIndentationGuides = IndentGuidesCheck.IsChecked == true;
        _working.MatchBrackets = MatchBracketsCheck.IsChecked == true;
        _working.ShowStatusBar = StatusBarCheck.IsChecked == true;
        _working.ShowWhitespace = WhitespaceCheck.IsChecked == true;
        _working.ShowEndOfLine = EndOfLineCheck.IsChecked == true;

        _working.TabSize = tabSize;
        _working.AutoSaveSeconds = autoSaveSeconds;
        _working.ConvertTabsToSpaces = ConvertTabsCheck.IsChecked == true;
        _working.WordWrap = WordWrapCheck.IsChecked == true;
        _working.EnableFolding = FoldingCheck.IsChecked == true;
        _working.AutoIndent = AutoIndentCheck.IsChecked == true;
        _working.TrimTrailingWhitespaceOnSave = TrimOnSaveCheck.IsChecked == true;
        _working.EnableTextDragDrop = DragDropCheck.IsChecked == true;
        _working.RestoreSession = RestoreSessionCheck.IsChecked == true;
        _working.AutoSaveRecovery = AutoRecoveryCheck.IsChecked == true;

        _working.DefaultEncoding = encoding.Kind;
        _working.DefaultLineEnding = lineEnding.Kind;
        _working.Normalize();
        return true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!GatherSettings(out var error))
        {
            MessageWindow.Inform(this, "Settings", "Some values need attention.", error ?? string.Empty, warning: true);
            return;
        }

        ResultSettings = _working;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

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
}
