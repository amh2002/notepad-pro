using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using NotepadPro.Core.Services;
using NotepadPro.Theming;

namespace NotepadPro.Dialogs;

public partial class AboutWindow : DialogWindow
{
    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"Version {version} (win-x64)";
        EngineText.Text = "AvalonEdit 6.3";
        RuntimeText.Text = $".NET {Environment.Version} · {RuntimeInformation.OSDescription}";
        ConfigText.Text = new SettingsService().ConfigDirectory;
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var text = $"Notepad Pro {VersionText.Text}\n{EngineText.Text}\n{RuntimeText.Text}\nConfig: {ConfigText.Text}";
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard can be locked by another process; diagnostics are non-critical.
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
