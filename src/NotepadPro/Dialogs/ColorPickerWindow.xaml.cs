using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NotepadPro.Core.Models;
using NotepadPro.Theming;

namespace NotepadPro.Dialogs;

public partial class ColorPickerWindow : DialogWindow
{
    private static readonly string[] Palette =
    [
        "#FFFFFF", "#F3F3F3", "#D4D4D4", "#A0A0A0", "#6B6B6B", "#3C3C3C", "#1E1E1E", "#0D1117", "#000000",
        "#F8D7DA", "#E06C75", "#C42B1C", "#FF7B72", "#E5C07B", "#D19A66", "#B8860B", "#9D5D00", "#FFC66D",
        "#D7F5D3", "#98C379", "#6CCB5F", "#0F7B0F", "#7EE787", "#4EC9B0", "#267F99", "#0550AE", "#58A6FF",
        "#DDEBF7", "#61AFEF", "#0067C0", "#0A3069", "#C678DD", "#8250DF", "#A31515", "#CE9178", "#FFF1A8"
    ];

    public ColorPickerWindow(string initialHex)
    {
        InitializeComponent();
        BuildPalette();
        HexBox.Text = NormalizeOrFallback(initialHex);
        UpdatePreview();

        Loaded += (_, _) =>
        {
            HexBox.Focus();
            HexBox.SelectAll();
        };
    }

    public string SelectedHex { get; private set; } = "#FFFFFF";

    private void BuildPalette()
    {
        foreach (var hex in Palette)
        {
            var border = new Border
            {
                Width = 38,
                Height = 26,
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("App.BorderBrush"),
                Background = ThemeService.Brush(hex),
                Cursor = Cursors.Hand,
                ToolTip = hex,
                Tag = hex
            };
            border.MouseLeftButtonUp += (_, _) =>
            {
                HexBox.Text = hex;
                HexBox.SelectAll();
            };
            border.MouseEnter += (_, _) => border.BorderBrush = (Brush)FindResource("App.AccentBrush");
            border.MouseLeave += (_, _) => border.BorderBrush = (Brush)FindResource("App.BorderBrush");
            PalettePanel.Children.Add(border);
        }
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var normalized = TryNormalize(HexBox.Text);
        if (normalized is not null)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            PreviewBorder.Background = ThemeService.Brush(normalized);
        }
        else
        {
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void UpdatePreview()
    {
        var normalized = TryNormalize(HexBox.Text);
        if (normalized is not null) PreviewBorder.Background = ThemeService.Brush(normalized);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var normalized = TryNormalize(HexBox.Text);
        if (normalized is null)
        {
            ErrorText.Visibility = Visibility.Visible;
            HexBox.Focus();
            HexBox.SelectAll();
            return;
        }

        SelectedHex = normalized;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string NormalizeOrFallback(string? value) => TryNormalize(value) ?? "#FFFFFF";

    private static string? TryNormalize(string? value)
    {
        if (!AppSettings.IsValidHexColor(value)) return null;
        var text = value!.Trim();
        if (!text.StartsWith('#')) text = "#" + text;
        if (text.Length == 4)
        {
            text = $"#{text[1]}{text[1]}{text[2]}{text[2]}{text[3]}{text[3]}";
        }
        return text.ToUpperInvariant();
    }
}
