using System.IO;
using System.Windows;
using System.Windows.Threading;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;
using NotepadPro.Theming;
using NotepadPro.ViewModels;

namespace NotepadPro;

/// <summary>
/// Application entry point. Owns the long-lived services and the single main window, and
/// turns otherwise fatal UI exceptions into a diagnostic message instead of a silent crash.
/// </summary>
public partial class App : Application
{
    private bool _reportingException;

    public static new App Current => (App)Application.Current;

    public SettingsService SettingsService { get; private set; } = null!;
    public RecoveryService RecoveryService { get; private set; } = null!;
    public ThemeService ThemeService { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public MainViewModel ViewModel { get; private set; } = null!;
    public new MainWindow MainWindow { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        SettingsService = new SettingsService();
        RecoveryService = new RecoveryService(SettingsService.RecoveryDirectory);
        Settings = SettingsService.Load().Normalize();

        ThemeService = new ThemeService(this);
        ThemeService.Apply(Settings);

        ViewModel = new MainViewModel(SettingsService, RecoveryService, ThemeService, Settings);
        MainWindow = new MainWindow(ViewModel, e.Args);
        MainWindow.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (_reportingException) return;
        _reportingException = true;
        try
        {
            WriteErrorLog(e.Exception);
            MessageBox.Show(
                $"Notepad Pro hit an unexpected problem:\n\n{e.Exception.Message}\n\n" +
                "Your documents are still open. Save your work to a new location if the problem repeats.",
                "Notepad Pro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _reportingException = false;
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception) WriteErrorLog(exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteErrorLog(e.Exception);
        e.SetObserved();
    }

    private static void WriteErrorLog(Exception exception)
    {
        try
        {
            var directory = SettingsService.GetDataDirectory();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "error.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}\n\n");
        }
        catch
        {
            // Diagnostics must never make an error worse.
        }
    }
}
