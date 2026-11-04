using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;
using NotepadPro.Dialogs;
using NotepadPro.Editor;
using NotepadPro.Theming;
using NotepadPro.ViewModels;

namespace NotepadPro;

/// <summary>
/// Main application window. Owns the shared AvalonEdit surface and coordinates the
/// document collection, file commands, search overlay, settings, recovery and session state.
/// </summary>
public partial class MainWindow : Window
{
    private sealed record ClosedTab(
        string? FilePath,
        string Title,
        string Text,
        TextEncodingKind Encoding,
        LineEndingKind LineEnding,
        LanguageDefinition Language,
        int CaretOffset,
        double VerticalOffset,
        double HorizontalOffset,
        bool WasDirty,
        IReadOnlyList<int> FoldedOffsets);

    private readonly MainViewModel _viewModel;
    private readonly string[] _startupArguments;
    private readonly EditorHost _editorHost;
    private readonly SyntaxHighlightingService _syntaxService = new();
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly DispatcherTimer _searchTimer;
    private readonly DispatcherTimer _externalWatchTimer;
    private readonly List<ClosedTab> _closedTabs = [];
    private readonly List<MenuItem> _languageItems = [];
    private readonly List<MenuItem> _schemeItems = [];
    private readonly List<(int Start, int Length)> _searchMatches = [];
    private readonly HashSet<string> _externalChangeNotified = new(StringComparer.OrdinalIgnoreCase);

    private DocumentViewModel? _activeDocument;
    private double _zoomPercent = 100;
    private int _currentMatchIndex = -1;
    private string _searchKey = string.Empty;
    private bool _suppressSearch;
    private bool _sessionRestored;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel, string[]? startupArguments = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _startupArguments = startupArguments ?? [];

        InitializeComponent();
        DataContext = _viewModel;

        _editorHost = new EditorHost(Editor);
        _editorHost.CaretChanged += EditorHost_CaretChanged;
        _editorHost.DocumentMutated += EditorHost_DocumentMutated;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.Documents.CollectionChanged += Documents_CollectionChanged;
        _viewModel.ThemeService.Changed += ThemeService_Changed;

        _autoSaveTimer = new DispatcherTimer();
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchTimer.Tick += SearchTimer_Tick;

        _externalWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _externalWatchTimer.Tick += ExternalWatchTimer_Tick;

        ApplyWindowPlacement();
        ApplySettingsToUi(save: false);
        BuildLanguageMenu();
        BuildSchemeMenu();
        BuildRecentMenu();
        UpdateMenuChecks();
    }

    private AppSettings Settings => _viewModel.Settings;

    // ---------------------------------------------------------------- startup

    private void ApplyWindowPlacement()
    {
        var placement = Settings.Window;
        if (placement.Left is double left && placement.Top is double top)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (placement.Width >= MinWidth) Width = placement.Width;
        if (placement.Height >= MinHeight) Height = placement.Height;
        if (placement.Maximized) WindowState = WindowState.Maximized;
    }

    private void CaptureWindowPlacement()
    {
        var bounds = RestoreBounds;
        if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
        {
            Settings.Window.Width = bounds.Width;
            Settings.Window.Height = bounds.Height;
            Settings.Window.Left = bounds.Left;
            Settings.Window.Top = bounds.Top;
        }
        Settings.Window.Maximized = WindowState == WindowState.Maximized;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_sessionRestored) return;
        _sessionRestored = true;

        try
        {
            await RestoreStartupStateAsync();
            RestartAutoSaveTimer();
            _externalWatchTimer.Start();

            if (_viewModel.SettingsService.LastLoadError is { Length: > 0 } error)
            {
                ShowInfo($"A configuration file could not be read and defaults were used: {error}", warning: true);
            }
        }
        catch (Exception ex)
        {
            ShowInfo($"Could not restore the previous session: {ex.Message}", warning: true);
            if (_viewModel.Documents.Count == 0) _viewModel.CreateDocument(activate: true);
        }

        Editor.Focus();
        UpdateCommandState();
    }

    private async Task RestoreStartupStateAsync()
    {
        var commandLineFiles = _startupArguments
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToArray();

        if (commandLineFiles.Length > 0)
        {
            DocumentViewModel? last = null;
            foreach (var path in commandLineFiles)
            {
                last = await OpenFileAsync(path, activate: false) ?? last;
            }
            if (last is not null) _viewModel.Activate(last);
            if (_viewModel.Documents.Count == 0) _viewModel.CreateDocument(activate: true);
            return;
        }

        var snapshots = await _viewModel.RecoveryService.LoadAsync();
        if (snapshots.Count > 0)
        {
            var count = snapshots.Count;
            var restore = MessageWindow.Confirm(
                this,
                "Recover unsaved work",
                $"Notepad Pro found {count} unsaved document{(count == 1 ? string.Empty : "s")} from a previous session.",
                "Recover the text now? Choosing Discard deletes the recovered snapshots.",
                "Restore",
                "Discard",
                warning: true);

            if (restore)
            {
                foreach (var snapshot in snapshots) RestoreSnapshot(snapshot);
            }
            else
            {
                await _viewModel.RecoveryService.ClearAsync();
            }
        }

        if (Settings.RestoreSession)
        {
            await RestoreSessionDocumentsAsync();
        }

        if (_viewModel.Documents.Count == 0)
        {
            _viewModel.CreateDocument(activate: true);
        }
        else if (_viewModel.ActiveDocument is null)
        {
            _viewModel.Activate(_viewModel.Documents[0]);
        }
    }

    private void RestoreSnapshot(RecoverySnapshot snapshot)
    {
        var metadata = snapshot.Metadata;
        var language = LanguageCatalog.GetById(metadata.LanguageId);
        var document = _viewModel.CreateDocument(
            metadata.FilePath,
            snapshot.Text,
            metadata.Encoding,
            metadata.LineEnding,
            language,
            metadata.Title,
            activate: false);

        document.CaretOffset = metadata.CaretOffset;
        document.VerticalOffset = metadata.VerticalOffset;
        document.HorizontalOffset = metadata.HorizontalOffset;
        document.MarkAsRecovered();
    }

    private async Task RestoreSessionDocumentsAsync()
    {
        var session = _viewModel.SettingsService.LoadSession();
        if (session is null || session.Documents.Count == 0) return;

        var restored = new List<DocumentViewModel>();
        foreach (var entry in session.Documents)
        {
            if (string.IsNullOrWhiteSpace(entry.FilePath)) continue;
            if (!File.Exists(entry.FilePath)) continue;
            if (_viewModel.Documents.Any(document => document.FilePath is not null &&
                    string.Equals(document.FilePath, entry.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var content = await _viewModel.FileService.LoadAsync(entry.FilePath);
                var document = _viewModel.CreateDocument(
                    entry.FilePath,
                    content.Text,
                    entry.Encoding,
                    entry.LineEnding,
                    LanguageCatalog.GetById(entry.LanguageId),
                    lastWriteTimeUtc: content.LastWriteTimeUtc,
                    activate: false);

                document.CaretOffset = entry.CaretOffset;
                document.VerticalOffset = entry.VerticalOffset;
                document.HorizontalOffset = entry.HorizontalOffset;
                restored.Add(document);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // A session entry that cannot be read should not stop the rest of the restore.
            }
        }

        if (restored.Count == 0) return;
        var activeIndex = Math.Clamp(session.ActiveIndex, 0, restored.Count - 1);
        _viewModel.Activate(restored[activeIndex]);
        foreach (var document in restored) _viewModel.AddRecentFile(document.FilePath!);
        BuildRecentMenu();
    }

    private void RestartAutoSaveTimer()
    {
        _autoSaveTimer.Stop();
        var seconds = Math.Max(0, Settings.AutoSaveSeconds);
        if (!Settings.AutoSaveRecovery || seconds == 0) return;
        _autoSaveTimer.Interval = TimeSpan.FromSeconds(seconds);
        _autoSaveTimer.Start();
    }

    // ---------------------------------------------------------------- document lifecycle

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ActiveDocument)) OnActiveDocumentChanged();
    }

    private void Documents_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCommandState();
    }

    private void OnActiveDocumentChanged()
    {
        CaptureViewState();
        var document = _viewModel.ActiveDocument;

        if (ReferenceEquals(document, _activeDocument))
        {
            UpdateCommandState();
            return;
        }

        _activeDocument = document;
        if (document is null)
        {
            UpdateCommandState();
            return;
        }

        try
        {
            Editor.Document = document.Document;
        }
        catch (Exception ex)
        {
            ShowInfo($"Could not display '{document.Title}': {ex.Message}", warning: true);
            return;
        }

        _editorHost.ApplySettings(Settings);
        _editorHost.ApplyTheme(_viewModel.ThemeService);
        ApplyHighlighting(document);
        _editorHost.AttachFolding(document.Language);
        _editorHost.RestoreView(document.VerticalOffset, document.HorizontalOffset, document.CaretOffset);
        _editorHost.RestoreFoldedOffsets(document.FoldedOffsets);
        ApplyZoom();

        if (FindPanel.Visibility == Visibility.Visible)
        {
            _searchKey = string.Empty;
            RefreshSearchMatches();
        }
        else
        {
            _editorHost.ClearSearchMatches();
        }

        UpdateTitle();
        UpdateMenuChecks();
        UpdateCommandState();
        TabStrip.ScrollIntoView(document);
    }

    private void CaptureViewState()
    {
        if (_activeDocument is null) return;
        _activeDocument.CaretOffset = Editor.CaretOffset;
        _activeDocument.VerticalOffset = Editor.VerticalOffset;
        _activeDocument.HorizontalOffset = Editor.HorizontalOffset;
        _editorHost.CaptureFoldedOffsets(_activeDocument.FoldedOffsets);
    }

    private void UpdateTitle()
    {
        Title = _viewModel.ActiveDocument?.WindowTitle ?? "Notepad Pro";
    }

    private void NewTab()
    {
        _viewModel.CreateDocument(activate: true);
        Editor.Focus();
    }

    private async Task<bool> CloseDocumentAsync(DocumentViewModel document)
    {
        if (document.IsDirty)
        {
            var choice = UnsavedChangesWindow.Ask(this, document.Title, document.FilePath);
            if (choice == UnsavedChoice.Cancel) return false;
            if (choice == UnsavedChoice.Save && !await SaveDocumentAsync(document)) return false;
        }

        RememberClosedTab(document);
        _viewModel.RemoveDocument(document);
        return true;
    }

    private void RememberClosedTab(DocumentViewModel document)
    {
        CaptureViewState();
        _closedTabs.Add(new ClosedTab(
            document.FilePath,
            document.Title,
            document.Document.Text,
            document.Encoding,
            document.LineEnding,
            document.Language,
            document.CaretOffset,
            document.VerticalOffset,
            document.HorizontalOffset,
            document.IsDirty,
            [.. document.FoldedOffsets]));

        while (_closedTabs.Count > 20) _closedTabs.RemoveAt(0);
    }

    private async Task CloseDocumentsAsync(IEnumerable<DocumentViewModel> documents)
    {
        foreach (var document in documents.ToList())
        {
            if (!await CloseDocumentAsync(document)) return;
        }
    }

    // ---------------------------------------------------------------- file commands

    private async Task OpenFileInteractiveAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open",
            Filter = BuildFileFilter(),
            FilterIndex = 1,
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true) return;

        DocumentViewModel? last = null;
        foreach (var path in dialog.FileNames)
        {
            last = await OpenFileAsync(path, activate: true) ?? last;
        }
        if (last is not null) _viewModel.Activate(last);
        Editor.Focus();
    }

    private async Task<DocumentViewModel?> OpenFileAsync(string path, bool activate = true)
    {
        try
        {
            path = Path.GetFullPath(path);
            var existing = _viewModel.Documents.FirstOrDefault(document => document.FilePath is not null &&
                string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (activate) _viewModel.Activate(existing);
                ShowInfo($"'{existing.Title}' is already open.");
                return existing;
            }

            if (!File.Exists(path))
            {
                ShowInfo($"File not found: {path}", warning: true);
                return null;
            }

            var content = await _viewModel.FileService.LoadAsync(path);
            var document = _viewModel.CreateDocument(
                path,
                content.Text,
                content.Encoding,
                content.LineEnding,
                LanguageCatalog.DetectFromPath(path),
                lastWriteTimeUtc: content.LastWriteTimeUtc,
                activate: activate);

            _viewModel.AddRecentFile(path);
            BuildRecentMenu();
            UpdateCommandState();
            return document;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowInfo($"Could not open '{Path.GetFileName(path)}': {ex.Message}", warning: true);
            return null;
        }
    }

    private async Task<bool> SaveDocumentAsync(DocumentViewModel document, bool saveAs = false)
    {
        var path = document.FilePath;
        if (saveAs || string.IsNullOrEmpty(path))
        {
            var dialog = new SaveFileDialog
            {
                Title = saveAs ? "Save as" : "Save",
                Filter = BuildFileFilter(),
                FilterIndex = 1,
                FileName = document.Title,
                DefaultExt = ".txt",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }

        try
        {
            if (Settings.TrimTrailingWhitespaceOnSave) TrimDocument(document);

            var previousPath = document.FilePath;
            var result = await _viewModel.FileService.SaveAsync(
                path,
                document.Document.Text,
                document.Encoding,
                document.LineEnding);

            document.MarkSaved(path, document.Encoding, document.LineEnding);
            document.SetLastWriteTime(result.LastWriteTimeUtc);
            _externalChangeNotified.Remove(path);
            _viewModel.AddRecentFile(path);

            if (!string.Equals(previousPath, document.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                var detected = LanguageCatalog.DetectFromPath(document.FilePath);
                if (!detected.IsPlainText)
                {
                    document.SetLanguage(detected);
                    if (ReferenceEquals(document, _activeDocument))
                    {
                        ApplyHighlighting(document);
                        _editorHost.AttachFolding(document.Language);
                    }
                }
            }

            BuildRecentMenu();
            UpdateMenuChecks();
            UpdateCommandState();
            UpdateTitle();
            ShowInfo($"Saved '{document.Title}'.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            ShowInfo($"Could not save '{Path.GetFileName(path)}': {ex.Message}", warning: true);
            return false;
        }
    }

    private static void TrimDocument(DocumentViewModel document)
    {
        var original = document.Document.Text;
        var trimmed = FileService.TrimTrailingWhitespace(original);
        if (trimmed == original) return;
        using (document.Document.RunUpdate())
        {
            document.Document.Replace(0, document.Document.TextLength, trimmed);
        }
    }

    private async Task ReloadDocumentAsync(DocumentViewModel? document)
    {
        if (document is null) return;
        if (document.FilePath is null)
        {
            ShowInfo("This document has not been saved yet, so there is nothing to reload.", warning: true);
            return;
        }

        if (document.IsDirty)
        {
            var reload = MessageWindow.Confirm(
                this,
                "Reload from disk",
                $"Reload '{document.Title}'?",
                "The file on disk will replace the version in the editor. Unsaved changes in this tab will be lost.",
                "Reload",
                warning: true);
            if (!reload) return;
        }

        try
        {
            var content = await _viewModel.FileService.LoadAsync(document.FilePath);
            document.ReplaceContent(content.Text);
            document.SetEncoding(content.Encoding);
            document.SetLineEnding(content.LineEnding);
            document.SetLastWriteTime(content.LastWriteTimeUtc);
            _externalChangeNotified.Remove(document.FilePath);

            if (ReferenceEquals(document, _activeDocument))
            {
                ApplyHighlighting(document);
                _editorHost.AttachFolding(document.Language);
            }

            UpdateTitle();
            UpdateCommandState();
            ShowInfo($"Reloaded '{document.Title}' from disk.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowInfo($"Could not reload '{document.Title}': {ex.Message}", warning: true);
        }
    }

    private static string BuildFileFilter()
    {
        var extensions = LanguageCatalog.All
            .SelectMany(language => language.Extensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
            .Select(extension => "*" + extension);

        return $"Text and code files|{string.Join(";", extensions)}|All files|*.*|Text files|*.txt";
    }

    private void ReopenClosedTab()
    {
        if (_closedTabs.Count == 0)
        {
            ShowInfo("There are no closed tabs to reopen.");
            return;
        }

        var tab = _closedTabs[^1];
        _closedTabs.RemoveAt(_closedTabs.Count - 1);

        var document = _viewModel.CreateDocument(
            tab.FilePath,
            tab.Text,
            tab.Encoding,
            tab.LineEnding,
            tab.Language,
            tab.FilePath is null ? tab.Title : null,
            activate: false);

        document.CaretOffset = tab.CaretOffset;
        document.VerticalOffset = tab.VerticalOffset;
        document.HorizontalOffset = tab.HorizontalOffset;
        foreach (var offset in tab.FoldedOffsets) document.FoldedOffsets.Add(offset);
        if (tab.WasDirty) document.MarkAsRecovered();

        _viewModel.Activate(document);
        Editor.Focus();
    }

    // ---------------------------------------------------------------- autosave, session and close

    private async void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await SaveRecoverySnapshotsAsync();
        }
        catch (Exception)
        {
            // Recovery is best-effort and must never interrupt editing.
        }
    }

    private async Task SaveRecoverySnapshotsAsync()
    {
        if (!Settings.AutoSaveRecovery || Settings.AutoSaveSeconds <= 0) return;
        CaptureViewState();

        var snapshots = new List<RecoverySnapshot>();
        foreach (var document in _viewModel.Documents)
        {
            if (!document.IsDirty) continue;
            snapshots.Add(new RecoverySnapshot(new RecoveryMetadata
            {
                Id = document.Id,
                FilePath = document.FilePath,
                Title = document.Title,
                LanguageId = document.Language.Id,
                Encoding = document.Encoding,
                LineEnding = document.LineEnding,
                IsDirty = true,
                CaretOffset = document.CaretOffset,
                VerticalOffset = document.VerticalOffset,
                HorizontalOffset = document.HorizontalOffset,
                SavedAtUtc = DateTime.UtcNow
            }, document.Document.Text));
        }

        await _viewModel.RecoveryService.SaveAsync(snapshots);
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;

        e.Cancel = true;
        _ = Dispatcher.BeginInvoke(new Action(RunConfirmClose));
    }

    private async void RunConfirmClose()
    {
        try
        {
            await ConfirmCloseAsync();
        }
        catch (Exception ex)
        {
            ShowInfo($"Could not close the application: {ex.Message}", warning: true);
        }
    }

    private async Task ConfirmCloseAsync()
    {
        if (_allowClose) return;

        CaptureViewState();

        foreach (var document in _viewModel.Documents.ToList())
        {
            if (!document.IsDirty) continue;
            var choice = UnsavedChangesWindow.Ask(this, document.Title, document.FilePath);
            if (choice == UnsavedChoice.Cancel) return;
            if (choice == UnsavedChoice.Save && !await SaveDocumentAsync(document)) return;
        }

        CaptureWindowPlacement();
        SaveSettingsQuietly();
        PersistSession();

        try
        {
            await _viewModel.RecoveryService.ClearAsync();
        }
        catch (Exception)
        {
            // Recovery cleanup failure must not block the user from closing.
        }

        _allowClose = true;
        Close();
    }

    private void PersistSession()
    {
        var session = new SessionState
        {
            ActiveIndex = 0,
            Window = Settings.Window,
            SavedAtUtc = DateTime.UtcNow
        };

        var activePath = _viewModel.ActiveDocument?.FilePath;
        foreach (var document in _viewModel.Documents)
        {
            if (document.FilePath is null) continue;
            session.Documents.Add(new SessionDocument
            {
                FilePath = document.FilePath,
                Title = document.Title,
                LanguageId = document.Language.Id,
                Encoding = document.Encoding,
                LineEnding = document.LineEnding,
                CaretOffset = document.CaretOffset,
                VerticalOffset = document.VerticalOffset,
                HorizontalOffset = document.HorizontalOffset,
                IsDirty = document.IsDirty,
                SnapshotId = null
            });
            if (activePath is not null &&
                string.Equals(document.FilePath, activePath, StringComparison.OrdinalIgnoreCase))
            {
                session.ActiveIndex = session.Documents.Count - 1;
            }
        }

        _viewModel.SettingsService.TrySaveSession(session, out _);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _autoSaveTimer.Stop();
        _searchTimer.Stop();
        _externalWatchTimer.Stop();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Documents.CollectionChanged -= Documents_CollectionChanged;
        _viewModel.ThemeService.Changed -= ThemeService_Changed;
        _editorHost.Dispose();
    }

    private void Window_Activated(object? sender, EventArgs e) => UpdateCommandState();

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeGlyph is null) return;
        MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    // ---------------------------------------------------------------- theme, settings and menus

    private void ApplySettingsToUi(bool save)
    {
        _zoomPercent = Math.Clamp(Settings.ZoomPercent, 50, 300);
        _editorHost.ApplySettings(Settings);
        _editorHost.ApplyTheme(_viewModel.ThemeService);
        ApplyZoom();
        UpdateStatusBarVisibility();

        if (_activeDocument is not null)
        {
            ApplyHighlighting(_activeDocument);
            _editorHost.AttachFolding(_activeDocument.Language);
            _editorHost.RestoreFoldedOffsets(_activeDocument.FoldedOffsets);
        }

        UpdateMenuChecks();

        if (save) SaveSettingsQuietly();
    }

    private void ApplyEditorOptions()
    {
        _editorHost.ApplySettings(Settings);
        ApplyZoom();
        UpdateStatusBarVisibility();
        UpdateMenuChecks();
        SaveSettingsQuietly();
    }

    private void UpdateStatusBarVisibility()
    {
        StatusBarBorder.Visibility = Settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyZoom()
    {
        var size = Math.Clamp(Settings.FontSize * (_zoomPercent / 100d), 6, 96);
        Editor.FontSize = size;
        ZoomButton.Content = $"{_zoomPercent:0}%";
    }

    private void SetZoom(double percent)
    {
        _zoomPercent = Math.Clamp(Math.Round(percent), 50, 300);
        Settings.ZoomPercent = _zoomPercent;
        ApplyZoom();
        SaveSettingsQuietly();
    }

    private void ApplyHighlighting(DocumentViewModel document)
    {
        if (document.Language.IsPlainText)
        {
            _editorHost.ApplyHighlighting(null);
            return;
        }

        _editorHost.ApplyHighlighting(_syntaxService.Get(document.Language, _viewModel.ThemeService.Scheme));
    }

    private void ThemeService_Changed(object? sender, ThemeChangedEventArgs e)
    {
        _editorHost.ApplyTheme(_viewModel.ThemeService);
        if (_activeDocument is not null) ApplyHighlighting(_activeDocument);
        UpdateMenuChecks();
    }

    private void BuildLanguageMenu()
    {
        LanguageMenu.Items.Clear();
        _languageItems.Clear();

        foreach (var language in LanguageCatalog.All)
        {
            var item = new MenuItem
            {
                Header = language.DisplayName,
                IsCheckable = true,
                Tag = language
            };
            item.Click += LanguageItem_Click;
            LanguageMenu.Items.Add(item);
            _languageItems.Add(item);
        }

        LanguageMenu.Items.Add(new Separator());
        var detect = new MenuItem { Header = "Detect from file _extension" };
        detect.Click += AutoDetectLanguage_Click;
        LanguageMenu.Items.Add(detect);
    }

    private void LanguageItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: LanguageDefinition language }) return;
        var document = _activeDocument;
        if (document is null) return;

        document.SetLanguage(language);
        ApplyHighlighting(document);
        _editorHost.AttachFolding(language);
        _editorHost.RestoreFoldedOffsets(document.FoldedOffsets);
        UpdateMenuChecks();
        UpdateCommandState();
    }

    private void AutoDetectLanguage_Click(object sender, RoutedEventArgs e)
    {
        var document = _activeDocument;
        if (document is null) return;
        var detected = document.FilePath is null
            ? LanguageCatalog.PlainText
            : LanguageCatalog.DetectFromPath(document.FilePath);
        document.SetLanguage(detected);
        ApplyHighlighting(document);
        _editorHost.AttachFolding(detected);
        UpdateMenuChecks();
    }

    private void BuildSchemeMenu()
    {
        SchemeMenu.Items.Clear();
        _schemeItems.Clear();

        var auto = new MenuItem
        {
            Header = "Auto (match theme)",
            IsCheckable = true,
            Tag = ColorSchemes.AutoId
        };
        auto.Click += SchemeItem_Click;
        SchemeMenu.Items.Add(auto);
        _schemeItems.Add(auto);
        SchemeMenu.Items.Add(new Separator());

        foreach (var scheme in ColorSchemes.All)
        {
            var item = new MenuItem
            {
                Header = scheme.DisplayName,
                IsCheckable = true,
                Tag = scheme.Id,
                ToolTip = scheme.Description
            };
            item.Click += SchemeItem_Click;
            SchemeMenu.Items.Add(item);
            _schemeItems.Add(item);
        }

        UpdateThemeChecks();
    }

    private void SchemeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string schemeId }) return;
        if (string.Equals(Settings.ColorScheme, schemeId, StringComparison.OrdinalIgnoreCase))
        {
            UpdateThemeChecks();
            return;
        }

        Settings.ColorScheme = schemeId;
        _viewModel.ThemeService.Apply(Settings);
        SaveSettingsQuietly();
        UpdateThemeChecks();
    }

    private void SetThemeMode(AppThemeMode mode)
    {
        if (Settings.Theme == mode)
        {
            UpdateThemeChecks();
            return;
        }

        Settings.Theme = mode;
        _viewModel.ThemeService.Apply(Settings);
        SaveSettingsQuietly();
        UpdateThemeChecks();
    }

    private void BuildRecentMenu()
    {
        RecentMenu.Items.Clear();

        if (Settings.RecentFiles.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = "No recent files", IsEnabled = false });
            return;
        }

        foreach (var path in Settings.RecentFiles)
        {
            var item = new MenuItem
            {
                Header = Path.GetFileName(path),
                ToolTip = path,
                Tag = path
            };
            item.Click += RecentFile_Click;
            RecentMenu.Items.Add(item);
        }

        RecentMenu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "Clear recent files" };
        clear.Click += ClearRecentMenu_Click;
        RecentMenu.Items.Add(clear);
    }

    private async void RecentFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path }) return;
        if (!File.Exists(path))
        {
            _viewModel.SettingsService.RemoveRecentFile(Settings, path);
            BuildRecentMenu();
            SaveSettingsQuietly();
            ShowInfo($"The recent file no longer exists: {path}", warning: true);
            return;
        }

        await OpenFileAsync(path);
    }

    private void ClearRecentMenu_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SettingsService.ClearRecentFiles(Settings);
        BuildRecentMenu();
        SaveSettingsQuietly();
    }

    private void UpdateMenuChecks()
    {
        UpdateThemeChecks();
        UpdateLanguageChecks();

        WordWrapMenuItem.IsChecked = Settings.WordWrap;
        LineNumbersMenuItem.IsChecked = Settings.ShowLineNumbers;
        CurrentLineMenuItem.IsChecked = Settings.HighlightCurrentLine;
        IndentGuidesMenuItem.IsChecked = Settings.ShowIndentationGuides;
        MatchBracketsMenuItem.IsChecked = Settings.MatchBrackets;
        WhitespaceMenuItem.IsChecked = Settings.ShowWhitespace;
        EndOfLineMenuItem.IsChecked = Settings.ShowEndOfLine;
        FoldingMenuItem.IsChecked = Settings.EnableFolding;
        StatusBarMenuItem.IsChecked = Settings.ShowStatusBar;

        UpdateStatusBarVisibility();
    }

    private void UpdateThemeChecks()
    {
        ThemeSystemMenuItem.IsChecked = Settings.Theme == AppThemeMode.System;
        ThemeLightMenuItem.IsChecked = Settings.Theme == AppThemeMode.Light;
        ThemeDarkMenuItem.IsChecked = Settings.Theme == AppThemeMode.Dark;

        foreach (var item in _schemeItems)
            item.IsChecked = item.Tag is string id &&
                             string.Equals(id, Settings.ColorScheme, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateLanguageChecks()
    {
        var languageId = _activeDocument?.Language.Id;
        foreach (var item in _languageItems)
            item.IsChecked = item.Tag is LanguageDefinition language &&
                             string.Equals(language.Id, languageId, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateCommandState()
    {
        var hasDocument = _activeDocument is not null;
        UndoMenuItem.IsEnabled = hasDocument && Editor.CanUndo;
        RedoMenuItem.IsEnabled = hasDocument && Editor.CanRedo;
        SaveMenuItem.IsEnabled = hasDocument;
        UpdateTitle();
    }

    private void SaveSettingsQuietly()
    {
        _viewModel.SettingsService.TrySave(Settings, out _);
    }

    private void ShowInfo(string message, bool warning = false)
    {
        InfoBarText.Text = message;
        InfoBar.Visibility = Visibility.Visible;
        InfoBarText.Foreground = warning
            ? (Brush)FindResource("App.DangerBrush")
            : (Brush)FindResource("App.TextPrimaryBrush");
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowInfo($"Could not open '{path}': {ex.Message}", warning: true);
        }
    }

    private void OpenDocumentFolder(DocumentViewModel? document)
    {
        if (document?.FilePath is null)
        {
            ShowInfo("This document has not been saved yet.", warning: true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{document.FilePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowInfo($"Could not open the containing folder: {ex.Message}", warning: true);
        }
    }

    private void CopyDocumentPath(DocumentViewModel? document)
    {
        if (document?.FilePath is null)
        {
            ShowInfo("This document has not been saved yet.", warning: true);
            return;
        }

        try
        {
            Clipboard.SetText(document.FilePath);
            ShowInfo("Full path copied to the clipboard.");
        }
        catch (Exception ex)
        {
            ShowInfo($"Could not copy the path: {ex.Message}", warning: true);
        }
    }

    private DocumentViewModel? SenderDocument(object sender)
    {
        if (sender is FrameworkElement { DataContext: DocumentViewModel document }) return document;
        return _activeDocument;
    }

    // ---------------------------------------------------------------- external file changes

    private void ExternalWatchTimer_Tick(object? sender, EventArgs e)
    {
        foreach (var document in _viewModel.Documents)
        {
            if (document.FilePath is null || document.LastWriteTimeUtc == DateTime.MinValue) continue;
            try
            {
                var info = new FileInfo(document.FilePath);
                if (!info.Exists) continue;
                if (info.LastWriteTimeUtc <= document.LastWriteTimeUtc.AddSeconds(0.75)) continue;
                if (!_externalChangeNotified.Add(document.FilePath)) continue;
                ShowInfo($"'{document.Title}' changed on disk. Use File > Reload from disk to load the newer version.");
            }
            catch (Exception)
            {
                // A temporarily locked or vanished file is reported again on the next tick.
            }
        }
    }

    // ---------------------------------------------------------------- editor events

    private void EditorHost_CaretChanged(object? sender, CaretChangedEventArgs e)
    {
        _activeDocument?.UpdateCaret(e.Line, e.Column, e.SelectionLength, e.SelectionLineCount, e.Overstrike);
        UpdateCommandState();
    }

    private void EditorHost_DocumentMutated(object? sender, EventArgs e)
    {
        _activeDocument?.RefreshStatistics();
        UpdateCommandState();

        if (FindPanel.Visibility != Visibility.Visible) return;
        _searchMatches.Clear();
        _currentMatchIndex = -1;
        _editorHost.ClearSearchMatches();
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    // ---------------------------------------------------------------- search

    private SearchOptions CurrentSearchOptions() => new()
    {
        MatchCase = MatchCaseToggle.IsChecked == true,
        WholeWord = WholeWordToggle.IsChecked == true,
        UseRegex = RegexToggle.IsChecked == true
    };

    private static string BuildSearchKey(string query, SearchOptions options) =>
        string.Concat(query, "\u0001", options.MatchCase, options.WholeWord, options.UseRegex);

    private void ShowFindPanel(bool replace)
    {
        FindPanel.Visibility = Visibility.Visible;
        ReplaceRow.Visibility = replace ? Visibility.Visible : Visibility.Collapsed;

        if (Editor.SelectionLength > 0)
        {
            var selected = Editor.Document.GetText(Editor.SelectionStart, Editor.SelectionLength);
            if (selected.Length is > 0 and <= 200 &&
                selected.IndexOfAny(['\r', '\n']) < 0 &&
                !string.Equals(selected, FindBox.Text, StringComparison.Ordinal))
            {
                _suppressSearch = true;
                FindBox.Text = selected;
                _suppressSearch = false;
                _searchKey = string.Empty;
            }
        }

        FindBox.Focus();
        FindBox.SelectAll();
        RefreshSearchMatches();
    }

    private void CloseFindPanel()
    {
        FindPanel.Visibility = Visibility.Collapsed;
        ReplaceRow.Visibility = Visibility.Collapsed;
        SearchErrorText.Text = string.Empty;
        SearchCountText.Text = string.Empty;
        _searchTimer.Stop();
        _searchMatches.Clear();
        _currentMatchIndex = -1;
        _searchKey = string.Empty;
        _editorHost.ClearSearchMatches();
        Editor.Focus();
    }

    private void RefreshSearchMatches()
    {
        if (FindPanel.Visibility != Visibility.Visible) return;

        var query = FindBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            _searchMatches.Clear();
            _currentMatchIndex = -1;
            _searchKey = string.Empty;
            SearchCountText.Text = string.Empty;
            SearchErrorText.Text = string.Empty;
            _editorHost.ClearSearchMatches();
            return;
        }

        var options = CurrentSearchOptions();
        var regex = SearchEngine.TryCreateRegex(query, options, out var error);
        if (regex is null)
        {
            _searchMatches.Clear();
            _currentMatchIndex = -1;
            SearchCountText.Text = string.Empty;
            SearchErrorText.Text = error ?? "Invalid search pattern.";
            _editorHost.ClearSearchMatches();
            return;
        }

        var matches = SearchEngine.FindAll(Editor.Document.Text, regex);
        var key = BuildSearchKey(query, options);
        if (!string.Equals(key, _searchKey, StringComparison.Ordinal))
        {
            _searchKey = key;
            _currentMatchIndex = FindNearestMatch(matches, Editor.CaretOffset);
        }

        _searchMatches.Clear();
        _searchMatches.AddRange(matches);
        SearchErrorText.Text = string.Empty;

        if (_searchMatches.Count == 0)
        {
            _currentMatchIndex = -1;
            SearchCountText.Text = "No results";
        }
        else
        {
            _currentMatchIndex = Math.Clamp(_currentMatchIndex, 0, _searchMatches.Count - 1);
            SearchCountText.Text = $"{_currentMatchIndex + 1:N0} of {_searchMatches.Count:N0}";
        }

        UpdateSearchHighlights();
    }

    private static int FindNearestMatch(IReadOnlyList<(int Start, int Length)> matches, int caretOffset)
    {
        if (matches.Count == 0) return -1;
        for (var index = 0; index < matches.Count; index++)
        {
            var (start, length) = matches[index];
            if (caretOffset >= start && caretOffset <= start + Math.Max(0, length)) return index;
            if (start > caretOffset) return index;
        }
        return 0;
    }

    private void UpdateSearchHighlights()
    {
        _editorHost.UpdateSearchMatches(_searchMatches, _currentMatchIndex);
    }

    private void GoToMatch(int index)
    {
        if (_searchMatches.Count == 0) return;
        _currentMatchIndex = (index % _searchMatches.Count + _searchMatches.Count) % _searchMatches.Count;

        var (start, length) = _searchMatches[_currentMatchIndex];
        start = Math.Clamp(start, 0, Editor.Document.TextLength);
        length = Math.Clamp(length, 0, Editor.Document.TextLength - start);
        Editor.Select(start, length);
        Editor.CaretOffset = start;
        Editor.TextArea.Caret.BringCaretToView();
        SearchCountText.Text = $"{_currentMatchIndex + 1:N0} of {_searchMatches.Count:N0}";
        UpdateSearchHighlights();
    }

    private void FindNextMatch()
    {
        if (FindPanel.Visibility != Visibility.Visible) ShowFindPanel(replace: false);
        if (_searchMatches.Count == 0 ||
            !string.Equals(_searchKey, BuildSearchKey(FindBox.Text, CurrentSearchOptions()), StringComparison.Ordinal))
        {
            _searchKey = string.Empty;
            RefreshSearchMatches();
        }

        if (_searchMatches.Count == 0) return;
        GoToMatch(_currentMatchIndex < 0 ? 0 : _currentMatchIndex + 1);
    }

    private void FindPreviousMatch()
    {
        if (FindPanel.Visibility != Visibility.Visible) ShowFindPanel(replace: false);
        if (_searchMatches.Count == 0 ||
            !string.Equals(_searchKey, BuildSearchKey(FindBox.Text, CurrentSearchOptions()), StringComparison.Ordinal))
        {
            _searchKey = string.Empty;
            RefreshSearchMatches();
        }

        if (_searchMatches.Count == 0) return;
        GoToMatch(_currentMatchIndex < 0 ? _searchMatches.Count - 1 : _currentMatchIndex - 1);
    }

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        RefreshSearchMatches();
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSearch) return;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void FindBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) FindPreviousMatch();
            else FindNextMatch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFindPanel();
            e.Handled = true;
        }
    }

    private void ReplaceBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ReplaceCurrentMatch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFindPanel();
            e.Handled = true;
        }
    }

    private void ReplaceCurrentMatch()
    {
        if (FindPanel.Visibility != Visibility.Visible) ShowFindPanel(replace: true);
        if (_searchMatches.Count == 0 ||
            !string.Equals(_searchKey, BuildSearchKey(FindBox.Text, CurrentSearchOptions()), StringComparison.Ordinal))
        {
            _searchKey = string.Empty;
            RefreshSearchMatches();
        }

        if (_searchMatches.Count == 0)
        {
            SearchErrorText.Text = "No matches to replace.";
            return;
        }

        var options = CurrentSearchOptions();
        var regex = SearchEngine.TryCreateRegex(FindBox.Text, options, out var error);
        if (regex is null)
        {
            SearchErrorText.Text = error ?? "Invalid search pattern.";
            return;
        }

        var index = Math.Clamp(_currentMatchIndex < 0 ? 0 : _currentMatchIndex, 0, _searchMatches.Count - 1);
        var (start, length) = _searchMatches[index];
        start = Math.Clamp(start, 0, Editor.Document.TextLength);
        length = Math.Clamp(length, 0, Editor.Document.TextLength - start);

        string replacement;
        if (options.UseRegex)
        {
            var match = regex.Match(Editor.Document.Text, start);
            try
            {
                replacement = match.Success && match.Index == start && match.Length == length
                    ? match.Result(ReplaceBox.Text)
                    : ReplaceBox.Text;
            }
            catch (ArgumentException)
            {
                replacement = ReplaceBox.Text;
            }
        }
        else
        {
            replacement = ReplaceBox.Text;
        }

        using (Editor.Document.RunUpdate())
        {
            Editor.Document.Replace(start, length, replacement);
        }

        Editor.CaretOffset = Math.Clamp(start + replacement.Length, 0, Editor.Document.TextLength);
        _searchKey = string.Empty;
        RefreshSearchMatches();

        if (_searchMatches.Count > 0)
        {
            var next = _searchMatches.FindIndex(match => match.Start >= Editor.CaretOffset);
            GoToMatch(next < 0 ? 0 : next);
        }
    }

    private void ReplaceAllMatches()
    {
        if (FindPanel.Visibility != Visibility.Visible) ShowFindPanel(replace: true);
        if (FindBox.Text.Length == 0)
        {
            SearchErrorText.Text = "Enter something to replace.";
            return;
        }

        var options = CurrentSearchOptions();
        var regex = SearchEngine.TryCreateRegex(FindBox.Text, options, out var error);
        if (regex is null)
        {
            SearchErrorText.Text = error ?? "Invalid search pattern.";
            return;
        }

        var input = Editor.Document.Text;
        var replaced = 0;
        string output;
        try
        {
            output = regex.Replace(input, match =>
            {
                replaced++;
                return options.UseRegex ? match.Result(ReplaceBox.Text) : ReplaceBox.Text;
            });
        }
        catch (ArgumentException ex)
        {
            SearchErrorText.Text = ex.Message;
            return;
        }
        catch (RegexMatchTimeoutException)
        {
            SearchErrorText.Text = "The regular expression took too long to evaluate.";
            return;
        }

        if (replaced == 0 || string.Equals(output, input, StringComparison.Ordinal))
        {
            SearchErrorText.Text = "No matches to replace.";
            return;
        }

        var caret = Editor.CaretOffset;
        using (Editor.Document.RunUpdate())
        {
            Editor.Document.Replace(0, Editor.Document.TextLength, output);
        }
        Editor.CaretOffset = Math.Clamp(caret, 0, Editor.Document.TextLength);

        _searchKey = string.Empty;
        RefreshSearchMatches();
        ShowInfo($"Replaced {replaced:N0} occurrence{(replaced == 1 ? string.Empty : "s")}.");
    }

    // ---------------------------------------------------------------- window chrome

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    // ---------------------------------------------------------------- generic menu/toolbar events

    private void TopMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        UpdateCommandState();
        if (ReferenceEquals(sender, FileMenu)) BuildRecentMenu();
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => NewTab();

    private async void OpenFile_Click(object sender, RoutedEventArgs e) => await OpenFileInteractiveAsync();

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDocument is not null) await SaveDocumentAsync(_activeDocument);
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDocument is not null) await SaveDocumentAsync(_activeDocument, saveAs: true);
    }

    private async void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        var documents = _viewModel.Documents.Where(document => document.IsDirty).ToList();
        if (documents.Count == 0)
        {
            ShowInfo("There are no modified documents to save.");
            return;
        }

        foreach (var document in documents)
        {
            if (!await SaveDocumentAsync(document)) return;
        }
        ShowInfo($"Saved {documents.Count:N0} document{(documents.Count == 1 ? string.Empty : "s")}.");
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDocument is not null) await CloseDocumentAsync(_activeDocument);
    }

    private void ReopenClosedTab_Click(object sender, RoutedEventArgs e) => ReopenClosedTab();

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private async void ReloadFromDisk_Click(object sender, RoutedEventArgs e) => await ReloadDocumentAsync(_activeDocument);

    private void OpenContainingFolder_Click(object sender, RoutedEventArgs e) => OpenDocumentFolder(_activeDocument);

    private void CopyFullPath_Click(object sender, RoutedEventArgs e) => CopyDocumentPath(_activeDocument);

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        Editor.Undo();
        UpdateCommandState();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        Editor.Redo();
        UpdateCommandState();
    }

    private void Cut_Click(object sender, RoutedEventArgs e) => Editor.Cut();

    private void Copy_Click(object sender, RoutedEventArgs e) => Editor.Copy();

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Editor.Paste();
        }
        catch (Exception ex)
        {
            ShowInfo($"Could not paste from the clipboard: {ex.Message}", warning: true);
        }
    }

    private void DeleteSelection_Click(object sender, RoutedEventArgs e) => Editor.Delete();

    private void SelectAll_Click(object sender, RoutedEventArgs e) => Editor.SelectAll();

    private void DuplicateLine_Click(object sender, RoutedEventArgs e) => TextEditingOperations.DuplicateLines(Editor);

    private void DeleteLine_Click(object sender, RoutedEventArgs e) => TextEditingOperations.DeleteLines(Editor);

    private void MoveLineUp_Click(object sender, RoutedEventArgs e) => TextEditingOperations.MoveLinesUp(Editor);

    private void MoveLineDown_Click(object sender, RoutedEventArgs e) => TextEditingOperations.MoveLinesDown(Editor);

    private void ToggleComment_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDocument is null) return;
        if (!TextEditingOperations.ToggleComment(Editor, _activeDocument.Language, out var message) &&
            message is not null)
        {
            ShowInfo(message, warning: true);
        }
    }

    private void Indent_Click(object sender, RoutedEventArgs e) =>
        TextEditingOperations.IndentSelection(Editor, Settings.TabSize, !Settings.ConvertTabsToSpaces);

    private void Outdent_Click(object sender, RoutedEventArgs e) =>
        TextEditingOperations.OutdentSelection(Editor, Settings.TabSize);

    private void UpperCase_Click(object sender, RoutedEventArgs e) => ConvertSelectionCase(CaseConversion.Upper);

    private void LowerCase_Click(object sender, RoutedEventArgs e) => ConvertSelectionCase(CaseConversion.Lower);

    private void TitleCase_Click(object sender, RoutedEventArgs e) => ConvertSelectionCase(CaseConversion.Title);

    private void SentenceCase_Click(object sender, RoutedEventArgs e) => ConvertSelectionCase(CaseConversion.Sentence);

    private void ConvertSelectionCase(CaseConversion conversion)
    {
        if (!TextEditingOperations.ConvertCase(Editor, conversion, out var message) && message is not null)
            ShowInfo(message, warning: true);
    }

    private void TrimWhitespace_Click(object sender, RoutedEventArgs e)
    {
        if (TextEditingOperations.TrimTrailingWhitespace(Editor))
            ShowInfo("Trailing whitespace removed.");
        else
            ShowInfo("No trailing whitespace found.");
    }

    private void InsertDateTime_Click(object sender, RoutedEventArgs e) => TextEditingOperations.InsertDateTime(Editor);

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomPercent + 10);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoomPercent - 10);

    private void ResetZoom_Click(object sender, RoutedEventArgs e) => SetZoom(100);

    private void ToggleWordWrap_Click(object sender, RoutedEventArgs e)
    {
        Settings.WordWrap = sender is MenuItem { IsCheckable: true } item
            ? item.IsChecked
            : !Settings.WordWrap;
        WordWrapMenuItem.IsChecked = Settings.WordWrap;
        ApplyEditorOptions();
    }

    private void ToggleLineNumbers_Click(object sender, RoutedEventArgs e)
    {
        Settings.ShowLineNumbers = LineNumbersMenuItem.IsChecked;
        ApplyEditorOptions();
    }

    private void ToggleCurrentLine_Click(object sender, RoutedEventArgs e)
    {
        Settings.HighlightCurrentLine = CurrentLineMenuItem.IsChecked;
        ApplyEditorOptions();
    }

    private void ToggleIndentGuides_Click(object sender, RoutedEventArgs e)
    {
        Settings.ShowIndentationGuides = IndentGuidesMenuItem.IsChecked;
        ApplyEditorOptions();
    }

    private void ToggleMatchBrackets_Click(object sender, RoutedEventArgs e)
    {
        Settings.MatchBrackets = MatchBracketsMenuItem.IsChecked;
        ApplyEditorOptions();
    }

    private void ToggleWhitespace_Click(object sender, RoutedEventArgs e)
    {
        Settings.ShowWhitespace = sender is MenuItem { IsCheckable: true } item
            ? item.IsChecked
            : !Settings.ShowWhitespace;
        WhitespaceMenuItem.IsChecked = Settings.ShowWhitespace;
        ApplyEditorOptions();
    }

    private void ToggleEndOfLine_Click(object sender, RoutedEventArgs e)
    {
        Settings.ShowEndOfLine = EndOfLineMenuItem.IsChecked;
        ApplyEditorOptions();
    }

    private void ToggleFolding_Click(object sender, RoutedEventArgs e)
    {
        Settings.EnableFolding = sender is MenuItem { IsCheckable: true } item
            ? item.IsChecked
            : !Settings.EnableFolding;
        FoldingMenuItem.IsChecked = Settings.EnableFolding;
        ApplyEditorOptions();

        if (_activeDocument is null) return;
        _editorHost.AttachFolding(_activeDocument.Language);
        _editorHost.RestoreFoldedOffsets(_activeDocument.FoldedOffsets);
    }

    private void ToggleStatusBar_Click(object sender, RoutedEventArgs e)
    {
        Settings.ShowStatusBar = sender is MenuItem { IsCheckable: true } item
            ? item.IsChecked
            : !Settings.ShowStatusBar;
        StatusBarMenuItem.IsChecked = Settings.ShowStatusBar;
        UpdateStatusBarVisibility();
        SaveSettingsQuietly();
    }

    private void Find_Click(object sender, RoutedEventArgs e) => ShowFindPanel(replace: false);

    private void Replace_Click(object sender, RoutedEventArgs e) => ShowFindPanel(replace: true);

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNextMatch();

    private void FindPrevious_Click(object sender, RoutedEventArgs e) => FindPreviousMatch();

    private void GoToLine_Click(object sender, RoutedEventArgs e)
    {
        if (_activeDocument is null) return;
        var lineCount = Editor.Document.LineCount;
        var currentLine = Editor.TextArea.Caret.Line;
        var dialog = new GoToLineWindow(lineCount, currentLine) { Owner = this };
        if (dialog.ShowDialog() == true) _editorHost.GoToLine(dialog.LineNumber);
    }

    private void ThemeSystem_Click(object sender, RoutedEventArgs e) => SetThemeMode(AppThemeMode.System);

    private void ThemeLight_Click(object sender, RoutedEventArgs e) => SetThemeMode(AppThemeMode.Light);

    private void ThemeDark_Click(object sender, RoutedEventArgs e) => SetThemeMode(AppThemeMode.Dark);

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(Settings, _viewModel.SettingsService) { Owner = this };
        if (window.ShowDialog() != true || window.ResultSettings is not { } updated) return;

        _viewModel.SetSettings(updated);
        _viewModel.ThemeService.Apply(Settings);
        ApplySettingsToUi(save: false);
        RestartAutoSaveTimer();
        SaveSettingsQuietly();
        ShowInfo("Settings updated.");
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(_viewModel.SettingsService.ConfigDirectory);

    private void OpenRecoveryFolder_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(_viewModel.SettingsService.RecoveryDirectory);

    private void ShowShortcuts_Click(object sender, RoutedEventArgs e) =>
        new ShortcutsWindow { Owner = this }.ShowDialog();

    private void ShowAbout_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void DismissInfoBar_Click(object sender, RoutedEventArgs e) => InfoBar.Visibility = Visibility.Collapsed;

    // ---------------------------------------------------------------- tab strip

    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (SenderDocument(sender) is { } document) _ = CloseDocumentAsync(document);
    }

    private void TabStrip_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        if (ItemsControl.ContainerFromElement(TabStrip, e.OriginalSource as DependencyObject) is ListBoxItem
            {
                DataContext: DocumentViewModel document
            })
        {
            _ = CloseDocumentAsync(document);
            e.Handled = true;
        }
    }

    private void TabStrip_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TabScrollViewer.ScrollToHorizontalOffset(TabScrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private async void TabContextSave_Click(object sender, RoutedEventArgs e)
    {
        if (SenderDocument(sender) is { } document) await SaveDocumentAsync(document);
    }

    private async void TabContextSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (SenderDocument(sender) is { } document) await SaveDocumentAsync(document, saveAs: true);
    }

    private async void TabContextClose_Click(object sender, RoutedEventArgs e)
    {
        if (SenderDocument(sender) is { } document) await CloseDocumentAsync(document);
    }

    private async void TabContextCloseOthers_Click(object sender, RoutedEventArgs e)
    {
        var keep = SenderDocument(sender);
        if (keep is null) return;
        var others = _viewModel.Documents.Where(document => !ReferenceEquals(document, keep)).ToList();
        foreach (var document in others)
        {
            if (!await CloseDocumentAsync(document)) return;
        }
    }

    private async void TabContextCloseAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var document in _viewModel.Documents.ToList())
        {
            if (!await CloseDocumentAsync(document)) return;
        }
    }

    private void TabContextCopyPath_Click(object sender, RoutedEventArgs e) => CopyDocumentPath(SenderDocument(sender));

    private void TabContextOpenFolder_Click(object sender, RoutedEventArgs e) => OpenDocumentFolder(SenderDocument(sender));

    private async void TabContextReload_Click(object sender, RoutedEventArgs e) =>
        await ReloadDocumentAsync(SenderDocument(sender));

    // ---------------------------------------------------------------- search panel UI

    private void ToggleReplaceRow_Click(object sender, RoutedEventArgs e)
    {
        var show = ReplaceRow.Visibility != Visibility.Visible;
        ReplaceRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            ReplaceBox.Focus();
            ReplaceBox.SelectAll();
        }
        else
        {
            FindBox.Focus();
        }
    }

    private void CloseFindPanel_Click(object sender, RoutedEventArgs e) => CloseFindPanel();

    private void ReplaceOne_Click(object sender, RoutedEventArgs e) => ReplaceCurrentMatch();

    private void ReplaceAll_Click(object sender, RoutedEventArgs e) => ReplaceAllMatches();

    private void SearchOptionChanged(object sender, RoutedEventArgs e)
    {
        if (FindPanel.Visibility != Visibility.Visible) return;
        _searchKey = string.Empty;
        RefreshSearchMatches();
    }

    // ---------------------------------------------------------------- status bar

    private void EncodingButton_Click(object sender, RoutedEventArgs e)
    {
        var document = _activeDocument;
        if (document is null) return;

        var menu = new ContextMenu { PlacementTarget = EncodingButton, Placement = PlacementMode.Top };
        foreach (var kind in TextEncodingKinds.All)
        {
            var item = new MenuItem
            {
                Header = kind.ToDisplayName(),
                IsCheckable = true,
                IsChecked = document.Encoding == kind,
                Tag = kind
            };
            item.Click += (_, _) =>
            {
                document.SetEncoding(kind);
                ShowInfo($"{document.Title} will be saved as {kind.ToDisplayName()}.");
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void LineEndingButton_Click(object sender, RoutedEventArgs e)
    {
        var document = _activeDocument;
        if (document is null) return;

        var menu = new ContextMenu { PlacementTarget = LineEndingButton, Placement = PlacementMode.Top };
        foreach (var kind in LineEndingKinds.All)
        {
            var item = new MenuItem
            {
                Header = kind.ToDisplayName(),
                IsCheckable = true,
                IsChecked = document.LineEnding == kind,
                Tag = kind
            };
            item.Click += (_, _) =>
            {
                document.SetLineEnding(kind);
                ShowInfo($"{document.Title} will be saved with {kind.ToDisplayName()} line endings.");
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        var document = _activeDocument;
        if (document is null) return;

        var menu = new ContextMenu { PlacementTarget = LanguageButton, Placement = PlacementMode.Top };
        foreach (var language in LanguageCatalog.All)
        {
            var item = new MenuItem
            {
                Header = language.DisplayName,
                IsCheckable = true,
                IsChecked = document.Language.Id == language.Id,
                Tag = language
            };
            item.Click += LanguageItem_Click;
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void InsertModeButton_Click(object sender, RoutedEventArgs e)
    {
        _editorHost.ToggleOverstrike();
        Editor.Focus();
    }

    private void ZoomButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ZoomButton, Placement = PlacementMode.Top };
        foreach (var percent in new[] { 50d, 75d, 100d, 125d, 150d, 175d, 200d, 300d })
        {
            var item = new MenuItem
            {
                Header = $"{percent:0}%",
                IsCheckable = true,
                IsChecked = Math.Abs(_zoomPercent - percent) < 0.5,
                Tag = percent
            };
            item.Click += (_, _) => SetZoom(percent);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var fit = new MenuItem { Header = "Reset to 100%" };
        fit.Click += (_, _) => SetZoom(100);
        menu.Items.Add(fit);
        menu.IsOpen = true;
    }

    // ---------------------------------------------------------------- drag and drop

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        DocumentViewModel? last = null;
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                ShowInfo("Folders cannot be opened as documents.", warning: true);
                continue;
            }
            last = await OpenFileAsync(path, activate: true) ?? last;
        }

        if (last is not null) _viewModel.Activate(last);
        e.Handled = true;
    }

    // ---------------------------------------------------------------- keyboard shortcuts

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var control = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;
        var alt = (modifiers & ModifierKeys.Alt) != 0;
        var editorFocused = Editor.IsKeyboardFocusWithin;

        if (key == Key.Escape && FindPanel.Visibility == Visibility.Visible)
        {
            CloseFindPanel();
            e.Handled = true;
            return;
        }

        if (control && alt && key == Key.S)
        {
            SaveAll_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (control && !alt)
        {
            switch (key)
            {
                case Key.N:
                    NewTab();
                    e.Handled = true;
                    return;
                case Key.O:
                    _ = OpenFileInteractiveAsync();
                    e.Handled = true;
                    return;
                case Key.S:
                    if (shift) SaveAs_Click(sender, e);
                    else Save_Click(sender, e);
                    e.Handled = true;
                    return;
                case Key.W:
                    if (shift) Close();
                    else CloseTab_Click(sender, e);
                    e.Handled = true;
                    return;
                case Key.T:
                    if (shift)
                    {
                        ReopenClosedTab();
                        e.Handled = true;
                    }
                    return;
                case Key.Tab:
                    if (_viewModel.ActiveDocument is { } active &&
                        _viewModel.GetNextDocument(active, shift ? -1 : 1) is { } next)
                    {
                        _viewModel.Activate(next);
                    }
                    e.Handled = true;
                    return;
                case Key.F:
                    ShowFindPanel(replace: false);
                    e.Handled = true;
                    return;
                case Key.H:
                    ShowFindPanel(replace: true);
                    e.Handled = true;
                    return;
                case Key.G:
                    GoToLine_Click(sender, e);
                    e.Handled = true;
                    return;
                case Key.OemPlus:
                case Key.Add:
                    SetZoom(_zoomPercent + 10);
                    e.Handled = true;
                    return;
                case Key.OemMinus:
                case Key.Subtract:
                    SetZoom(_zoomPercent - 10);
                    e.Handled = true;
                    return;
                case Key.D0:
                case Key.NumPad0:
                    SetZoom(100);
                    e.Handled = true;
                    return;
                case Key.OemTilde:
                    ToggleWordWrap_Click(sender, e);
                    e.Handled = true;
                    return;
                case Key.OemQuestion:
                case Key.Divide:
                    if (editorFocused)
                    {
                        ToggleComment_Click(sender, e);
                        e.Handled = true;
                    }
                    return;
                case Key.OemComma:
                    OpenSettings_Click(sender, e);
                    e.Handled = true;
                    return;
                case Key.D8:
                    if (shift)
                    {
                        ToggleWhitespace_Click(sender, e);
                        e.Handled = true;
                    }
                    return;
                case Key.B:
                    if (shift)
                    {
                        ToggleStatusBar_Click(sender, e);
                        e.Handled = true;
                    }
                    return;
                case Key.K:
                    if (shift && editorFocused)
                    {
                        DeleteLine_Click(sender, e);
                        e.Handled = true;
                    }
                    return;
                case Key.D:
                    if (!shift && editorFocused)
                    {
                        DuplicateLine_Click(sender, e);
                        e.Handled = true;
                    }
                    return;
                case Key.Z:
                    if (editorFocused && !shift)
                    {
                        Undo_Click(sender, e);
                        e.Handled = true;
                    }
                    return;
                case Key.Y:
                    if (editorFocused && !shift)
                    {
                        Redo_Click(sender, e);
                        e.Handled = true;
                    }
                    return;
                case Key.A:
                    if (editorFocused)
                    {
                        Editor.SelectAll();
                        e.Handled = true;
                    }
                    return;
            }
        }

        if (!control && alt && editorFocused)
        {
            if (key == Key.Up)
            {
                TextEditingOperations.MoveLinesUp(Editor);
                e.Handled = true;
                return;
            }
            if (key == Key.Down)
            {
                TextEditingOperations.MoveLinesDown(Editor);
                e.Handled = true;
                return;
            }
        }

        if (!control && !alt)
        {
            switch (key)
            {
                case Key.F1:
                    ShowShortcuts_Click(sender, e);
                    e.Handled = true;
                    return;
                case Key.F3:
                    if (shift) FindPreviousMatch();
                    else FindNextMatch();
                    e.Handled = true;
                    return;
                case Key.F5:
                    TextEditingOperations.InsertDateTime(Editor);
                    e.Handled = true;
                    return;
                case Key.Tab:
                    if (shift && editorFocused)
                    {
                        TextEditingOperations.OutdentSelection(Editor, Settings.TabSize);
                        e.Handled = true;
                    }
                    return;
                case Key.OemBackslash:
                    return;
            }
        }

        if (control && !alt && !shift && editorFocused && key == Key.C)
        {
            // Let AvalonEdit perform its normal copy operation.
        }
    }
}
