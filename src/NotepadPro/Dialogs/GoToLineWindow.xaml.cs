using System.Globalization;
using System.Windows;

namespace NotepadPro.Dialogs;

public partial class GoToLineWindow : DialogWindow
{
    private readonly int _lineCount;

    public GoToLineWindow(int lineCount, int currentLine)
    {
        InitializeComponent();
        _lineCount = Math.Max(1, lineCount);
        RangeText.Text = $"Enter a number between 1 and {_lineCount:N0}.";
        LineBox.Text = Math.Clamp(currentLine, 1, _lineCount).ToString(CultureInfo.CurrentCulture);
        Loaded += (_, _) =>
        {
            LineBox.Focus();
            LineBox.SelectAll();
        };
    }

    public int LineNumber { get; private set; } = 1;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(LineBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var line) ||
            line < 1 || line > _lineCount)
        {
            ErrorText.Text = $"Enter a whole number from 1 to {_lineCount:N0}.";
            ErrorText.Visibility = Visibility.Visible;
            LineBox.Focus();
            LineBox.SelectAll();
            return;
        }

        LineNumber = line;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
