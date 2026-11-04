using System.Windows;
using System.Windows.Media;

namespace NotepadPro.Dialogs;

/// <summary>
/// Small themed replacement for the stock Win32 message box. Keeps confirmation prompts
/// consistent with the rest of the application in both light and dark themes.
/// </summary>
public partial class MessageWindow : DialogWindow
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public static bool Confirm(
        Window owner,
        string title,
        string headline,
        string message,
        string confirmText,
        string cancelText = "Cancel",
        bool warning = false)
    {
        var dialog = new MessageWindow
        {
            Owner = owner,
            Title = title
        };
        dialog.HeadlineText.Text = headline;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = confirmText;
        dialog.CancelButton.Content = cancelText;
        dialog.CancelButton.Visibility = Visibility.Visible;
        if (warning) dialog.ApplyWarningStyle();
        dialog.ShowDialog();
        return dialog.Result;
    }

    public static void Inform(Window owner, string title, string headline, string message, bool warning = false)
    {
        var dialog = new MessageWindow
        {
            Owner = owner,
            Title = title
        };
        dialog.HeadlineText.Text = headline;
        dialog.MessageText.Text = message;
        dialog.ConfirmButton.Content = "OK";
        dialog.CancelButton.Visibility = Visibility.Collapsed;
        if (warning) dialog.ApplyWarningStyle();
        dialog.ShowDialog();
    }

    public bool Result { get; private set; }

    private void ApplyWarningStyle()
    {
        IconGlyph.Text = "\uE7BA";
        IconGlyph.Foreground = TryFindResource("App.WarningBrush") as Brush;
        IconBackground.Background = TryFindResource("App.WarningBackgroundBrush") as Brush;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Result = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = false;
        DialogResult = false;
    }
}
