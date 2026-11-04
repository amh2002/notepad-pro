using System.ComponentModel;
using System.IO;
using ICSharpCode.AvalonEdit.Document;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;
using NotepadPro.Core.Utils;

namespace NotepadPro.ViewModels;

/// <summary>
/// One open tab. Owns its AvalonEdit document and all per-document state, so switching
/// tabs only has to re-attach the shared editor to another document.
/// </summary>
public sealed class DocumentViewModel : ObservableObject, IDisposable
{
    private string? _filePath;
    private string _untitledName;
    private bool _isDirty;
    private bool _isActive;
    private bool _isRecovery;
    private TextEncodingKind _encoding;
    private LineEndingKind _lineEnding;
    private LanguageDefinition _language;
    private int _caretLine = 1;
    private int _caretColumn = 1;
    private int _selectionLength;
    private int _selectionLineCount;
    private bool _overstrike;
    private bool _isReadOnly;
    private DateTime _lastWriteTimeUtc;
    private bool _isDisposed;

    public DocumentViewModel(
        string? filePath,
        string text,
        TextEncodingKind encoding,
        LineEndingKind lineEnding,
        LanguageDefinition? language = null,
        string? untitledName = null,
        DateTime? lastWriteTimeUtc = null)
    {
        Id = Guid.NewGuid().ToString("N");
        _filePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        _untitledName = untitledName ?? "Untitled";
        _encoding = encoding;
        _lineEnding = lineEnding;
        _language = language ?? (filePath is null ? LanguageCatalog.PlainText : LanguageCatalog.DetectFromPath(filePath));
        _lastWriteTimeUtc = lastWriteTimeUtc ?? DateTime.MinValue;

        Document = new TextDocument(text);
        Document.UndoStack.SizeLimit = 5_000;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        Document.UndoStack.PropertyChanged += OnUndoStackPropertyChanged;
        Document.TextChanged += OnDocumentTextChanged;
    }

    public string Id { get; }

    public TextDocument Document { get; }

    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertiesChanged(nameof(Title), nameof(DisplayTitle), nameof(WindowTitle),
                    nameof(TabToolTip), nameof(IsUntitled), nameof(IsRecovery));
            }
        }
    }

    public bool IsUntitled => FilePath is null;

    public string Title => FilePath is null ? _untitledName : Path.GetFileName(FilePath);

    public string DisplayTitle => IsDirty ? Title + "*" : Title;

    public string WindowTitle => $"{DisplayTitle} - Notepad Pro";

    public string TabToolTip => FilePath ?? $"{Title} (unsaved)";

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (!SetProperty(ref _isDirty, value)) return;
            OnPropertiesChanged(nameof(DisplayTitle), nameof(WindowTitle), nameof(TabToolTip),
                nameof(DirtyMarkerVisibility));
        }
    }

    public System.Windows.Visibility DirtyMarkerVisibility =>
        IsDirty ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    /// <summary>True when the document was restored from a crash snapshot rather than opened from disk.</summary>
    public bool IsRecovery
    {
        get => _isRecovery;
        private set
        {
            if (SetProperty(ref _isRecovery, value)) OnPropertyChanged(nameof(TabToolTip));
        }
    }

    public TextEncodingKind Encoding
    {
        get => _encoding;
        set
        {
            if (SetProperty(ref _encoding, value)) OnPropertyChanged(nameof(EncodingDisplay));
        }
    }

    public string EncodingDisplay => Encoding.ToShortName();

    public LineEndingKind LineEnding
    {
        get => _lineEnding;
        set
        {
            if (SetProperty(ref _lineEnding, value)) OnPropertyChanged(nameof(LineEndingDisplay));
        }
    }

    public string LineEndingDisplay => LineEnding.ToShortName();

    public LanguageDefinition Language
    {
        get => _language;
        private set
        {
            if (SetProperty(ref _language, value)) OnPropertyChanged(nameof(LanguageDisplay));
        }
    }

    public string LanguageDisplay => Language.DisplayName;

    public int CaretLine
    {
        get => _caretLine;
        private set
        {
            if (SetProperty(ref _caretLine, value)) OnPropertyChanged(nameof(CaretDisplay));
        }
    }

    public int CaretColumn
    {
        get => _caretColumn;
        private set
        {
            if (SetProperty(ref _caretColumn, value)) OnPropertyChanged(nameof(CaretDisplay));
        }
    }

    public string CaretDisplay => $"Ln {CaretLine}, Col {CaretColumn}";

    public int SelectionLength
    {
        get => _selectionLength;
        private set
        {
            if (SetProperty(ref _selectionLength, value)) OnPropertyChanged(nameof(SelectionDisplay));
        }
    }

    public int SelectionLineCount
    {
        get => _selectionLineCount;
        private set
        {
            if (SetProperty(ref _selectionLineCount, value)) OnPropertyChanged(nameof(SelectionDisplay));
        }
    }

    public string SelectionDisplay => SelectionLength <= 0
        ? string.Empty
        : SelectionLineCount > 1
            ? $"Sel {SelectionLength:N0} chars, {SelectionLineCount:N0} lines"
            : $"Sel {SelectionLength:N0} chars";

    public bool Overstrike
    {
        get => _overstrike;
        private set
        {
            if (SetProperty(ref _overstrike, value)) OnPropertyChanged(nameof(InsertModeDisplay));
        }
    }

    public string InsertModeDisplay => Overstrike ? "OVR" : "INS";

    public string LineCountDisplay => Document.LineCount == 1 ? "1 line" : $"{Document.LineCount:N0} lines";

    public string CharacterCountDisplay => Document.TextLength == 1
        ? "1 character"
        : $"{Document.TextLength:N0} characters";

    public bool IsReadOnly
    {
        get => _isReadOnly;
        private set => SetProperty(ref _isReadOnly, value);
    }

    public DateTime LastWriteTimeUtc
    {
        get => _lastWriteTimeUtc;
        private set => SetProperty(ref _lastWriteTimeUtc, value);
    }

    /// <summary>Folded section start offsets, preserved while the tab is inactive.</summary>
    public HashSet<int> FoldedOffsets { get; } = [];

    public int CaretOffset { get; set; }
    public double VerticalOffset { get; set; }
    public double HorizontalOffset { get; set; }

    public string Text => Document.Text;
    public int TextLength => Document.TextLength;
    public int LineCount => Document.LineCount;

    public void UpdateCaret(int line, int column, int selectionLength, int selectionLineCount, bool overstrike)
    {
        CaretLine = Math.Max(1, line);
        CaretColumn = Math.Max(1, column);
        Overstrike = overstrike;
        SelectionLength = Math.Max(0, selectionLength);
        SelectionLineCount = Math.Max(0, selectionLineCount);
    }

    public void RefreshStatistics() =>
        OnPropertiesChanged(nameof(LineCountDisplay), nameof(CharacterCountDisplay));

    public void SetLanguage(LanguageDefinition language)
    {
        if (Language.Id == language.Id) return;
        Language = language;
    }

    public void SetEncoding(TextEncodingKind encoding) => Encoding = encoding;

    public void SetLineEnding(LineEndingKind lineEnding) => LineEnding = lineEnding;

    public void SetLastWriteTime(DateTime value) => LastWriteTimeUtc = value;

    public void MarkAsRecovered()
    {
        IsRecovery = true;
        Document.UndoStack.DiscardOriginalFileMarker();
        RefreshDirtyState();
    }

    public void MarkSaved(string? filePath = null, TextEncodingKind? encoding = null,
        LineEndingKind? lineEnding = null)
    {
        if (filePath is not null)
        {
            FilePath = Path.GetFullPath(filePath);
            _untitledName = Path.GetFileName(filePath);
            OnPropertyChanged(nameof(Title));
        }

        if (encoding is not null) Encoding = encoding.Value;
        if (lineEnding is not null) LineEnding = lineEnding.Value;

        Document.UndoStack.MarkAsOriginalFile();
        RefreshDirtyState();
        OnPropertiesChanged(nameof(Title), nameof(DisplayTitle), nameof(WindowTitle), nameof(TabToolTip));
    }

    /// <summary>Replaces all text without creating undo history (used by Revert and encoding changes).</summary>
    public void ReplaceContent(string text)
    {
        Document.Text = text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        RefreshDirtyState();
        RefreshStatistics();
    }

    public void RenameUntitled(string name)
    {
        if (_untitledName == name) return;
        _untitledName = name;
        OnPropertiesChanged(nameof(Title), nameof(DisplayTitle), nameof(WindowTitle), nameof(TabToolTip));
    }

    public void RefreshDirtyState()
    {
        var dirty = !Document.UndoStack.IsOriginalFile;
        if (dirty != IsDirty) IsDirty = dirty;
    }

    private void OnUndoStackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UndoStack.IsOriginalFile)) RefreshDirtyState();
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        RefreshDirtyState();
        RefreshStatistics();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Document.UndoStack.PropertyChanged -= OnUndoStackPropertyChanged;
        Document.TextChanged -= OnDocumentTextChanged;
    }
}
