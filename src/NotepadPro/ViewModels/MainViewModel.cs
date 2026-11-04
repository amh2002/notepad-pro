using System.Collections.ObjectModel;
using System.Collections.Specialized;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;
using NotepadPro.Core.Utils;
using NotepadPro.Theming;

namespace NotepadPro.ViewModels;

/// <summary>Owns the open document set, user preferences and the services shared by the UI.</summary>
public sealed class MainViewModel : ObservableObject
{
    private DocumentViewModel? _activeDocument;
    private int _untitledCounter;

    public MainViewModel(
        SettingsService settingsService,
        RecoveryService recoveryService,
        ThemeService themeService,
        AppSettings settings)
    {
        SettingsService = settingsService;
        RecoveryService = recoveryService;
        ThemeService = themeService;
        Settings = settings;
        Documents.CollectionChanged += OnDocumentsChanged;
    }

    public SettingsService SettingsService { get; }
    public RecoveryService RecoveryService { get; }
    public ThemeService ThemeService { get; }
    public FileService FileService { get; } = new();

    public AppSettings Settings { get; private set; }

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public DocumentViewModel? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (ReferenceEquals(_activeDocument, value)) return;
            if (_activeDocument is not null) _activeDocument.IsActive = false;
            _activeDocument = value;
            if (_activeDocument is not null) _activeDocument.IsActive = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDocuments));
            OnPropertyChanged(nameof(ActiveDocumentOrNull));
        }
    }

    public bool HasDocuments => Documents.Count > 0;

    public DocumentViewModel? ActiveDocumentOrNull => ActiveDocument;

    public event EventHandler? DocumentsChanged;

    public DocumentViewModel CreateDocument(
        string? filePath = null,
        string text = "",
        TextEncodingKind? encoding = null,
        LineEndingKind? lineEnding = null,
        LanguageDefinition? language = null,
        string? untitledName = null,
        DateTime? lastWriteTimeUtc = null,
        bool activate = true)
    {
        var document = new DocumentViewModel(
            filePath,
            text,
            encoding ?? Settings.DefaultEncoding,
            lineEnding ?? Settings.DefaultLineEnding,
            language,
            filePath is null ? untitledName ?? NextUntitledName() : untitledName,
            lastWriteTimeUtc);

        Documents.Add(document);
        if (activate) ActiveDocument = document;
        return document;
    }

    public string NextUntitledName()
    {
        _untitledCounter++;
        return _untitledCounter == 1 ? "Untitled" : $"Untitled {_untitledCounter}";
    }

    public void Activate(DocumentViewModel document)
    {
        if (!Documents.Contains(document)) return;
        ActiveDocument = document;
    }

    public DocumentViewModel? GetNextDocument(DocumentViewModel document, int direction = 1)
    {
        if (Documents.Count <= 1) return document;
        var index = Documents.IndexOf(document);
        if (index < 0) return ActiveDocument;
        var next = (index + direction + Documents.Count) % Documents.Count;
        return Documents[next];
    }

    public void RemoveDocument(DocumentViewModel document)
    {
        var index = Documents.IndexOf(document);
        if (index < 0) return;

        var wasActive = ReferenceEquals(ActiveDocument, document);
        Documents.RemoveAt(index);
        document.Dispose();

        if (wasActive)
        {
            ActiveDocument = Documents.Count == 0
                ? null
                : Documents[Math.Clamp(index, 0, Documents.Count - 1)];
        }

        if (Documents.Count == 0)
        {
            // Notepad always leaves an editing surface available.
            CreateDocument(activate: true);
        }
    }

    public SessionState BuildSession() => new()
    {
        ActiveIndex = ActiveDocument is null ? 0 : Math.Max(0, Documents.IndexOf(ActiveDocument)),
        Documents = Documents.Select(document => new SessionDocument
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
            SnapshotId = document.IsDirty ? document.Id : null
        }).ToList(),
        Window = Settings.Window
    };

    public void SetSettings(AppSettings settings)
    {
        Settings = settings;
        OnPropertyChanged(nameof(Settings));
    }

    public void AddRecentFile(string path)
    {
        SettingsService.AddRecentFile(Settings, path);
        OnPropertyChanged(nameof(Settings));
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasDocuments));
        DocumentsChanged?.Invoke(this, EventArgs.Empty);
    }
}
