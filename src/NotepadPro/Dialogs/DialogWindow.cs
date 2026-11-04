using System.Windows;
using System.Windows.Shell;

namespace NotepadPro.Dialogs;

/// <summary>
/// Shared window chrome for application dialogs. Keeps the dark/light theme consistent with
/// the main window while remaining a normal modal WPF window.
/// </summary>
public class DialogWindow : Window
{
    public DialogWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        SetResourceReference(BackgroundProperty, "App.WindowBrush");
        SetResourceReference(ForegroundProperty, "App.TextPrimaryBrush");

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 36,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });
    }
}
