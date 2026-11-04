using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using NotepadPro.Core.Models;
using NotepadPro.Theming;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NotepadPro.Editor;

/// <summary>Wraps a shared AvalonEdit TextEditor with our custom renderers, folding, indentation and search hooks.</summary>
public sealed class EditorHost : IDisposable
{
    private readonly TextEditor _editor;
    private readonly TextView _textView;
    private readonly TextArea _textArea;
    private FoldingManager? _foldingManager;
    private XmlFoldingStrategy? _xmlStrategy;
    private BraceFoldingStrategy? _braceStrategy;
    private IndentationFoldingStrategy? _indentationStrategy;
    private MarkdownFoldingStrategy? _markdownStrategy;
    private HashSet<int>? _pendingFoldedOffsets;
    private IndentationGuideRenderer? _indentGuide;
    private BracketHighlightRenderer? _bracketHighlight;
    private SearchHighlightRenderer? _searchHighlight;
    private SmartIndentationStrategy? _smartIndent;
    private IHighlightingDefinition? _currentDefinition;
    private DispatcherTimer? _foldingTimer;
    private AppSettings _settings = new();
    private bool _suppressCaret;
    private bool _disposed;

    public EditorHost(TextEditor editor)
    {
        _editor = editor;
        _textView = editor.TextArea.TextView;
        _textArea = editor.TextArea;
        _editor.Options.EnableTextDragDrop = true;
        _editor.Options.AllowToggleOverstrikeMode = true;
        _editor.TextArea.OverstrikeMode = false;

        _smartIndent = new SmartIndentationStrategy(
            () => _settings.AutoIndent,
            () => _settings.TabSize,
            () => _settings.ConvertTabsToSpaces);
        _editor.TextArea.IndentationStrategy = _smartIndent;

        _indentGuide = new IndentationGuideRenderer { IndentationSize = _settings.TabSize };
        _textView.BackgroundRenderers.Add(_indentGuide);

        _bracketHighlight = new BracketHighlightRenderer { IsEnabled = _settings.MatchBrackets };
        _textView.BackgroundRenderers.Add(_bracketHighlight);

        _searchHighlight = new SearchHighlightRenderer();
        _textView.BackgroundRenderers.Add(_searchHighlight);

        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _editor.TextArea.SelectionChanged += OnSelectionChanged;
        _editor.TextChanged += OnTextChanged;
        _editor.DocumentChanged += OnDocumentChanged;

        _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _foldingTimer.Tick += OnFoldingTimerTick;
    }

    public TextEditor Editor => _editor;
    public TextArea TextArea => _textArea;
    public TextView TextView => _textView;

    public event EventHandler<CaretChangedEventArgs>? CaretChanged;
    public event EventHandler? DocumentMutated;

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _editor.FontFamily = new FontFamily(string.IsNullOrWhiteSpace(settings.FontFamily) ? "Cascadia Mono" : settings.FontFamily);
        _editor.FontSize = settings.FontSize;
        _editor.Options.ShowSpaces = settings.ShowWhitespace;
        _editor.Options.ShowTabs = settings.ShowWhitespace;
        _editor.Options.ShowEndOfLine = settings.ShowEndOfLine;
        _editor.Options.HighlightCurrentLine = settings.HighlightCurrentLine;
        _editor.Options.ConvertTabsToSpaces = settings.ConvertTabsToSpaces;
        _editor.Options.IndentationSize = settings.TabSize;
        _editor.Options.EnableTextDragDrop = settings.EnableTextDragDrop;
        _editor.WordWrap = settings.WordWrap;
        _editor.ShowLineNumbers = settings.ShowLineNumbers;

        if (_indentGuide is not null)
        {
            _indentGuide.IsEnabled = settings.ShowIndentationGuides;
            _indentGuide.IndentationSize = settings.TabSize;
        }
        if (_bracketHighlight is not null)
            _bracketHighlight.IsEnabled = settings.MatchBrackets;

        _textView.InvalidateVisual();
    }

    public void ApplyTheme(ThemeService themeService)
    {
        var scheme = themeService.Scheme;
        _editor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.Background));
        _editor.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.Foreground));
        _editor.LineNumbersForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.LineNumber));
        _textArea.SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.Selection)) { Opacity = 0.8 };
        _textArea.SelectionBorder = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.SelectionBorder)), 1);
        _textArea.Caret.CaretBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.Caret));
        _textView.CurrentLineBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.CurrentLine));
        _textView.CurrentLineBorder = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.CurrentLineBorder)), 1);
        _textView.LinkTextUnderline = false;

        if (_indentGuide is not null)
            _indentGuide.GuideBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.IndentGuide));
        if (_bracketHighlight is not null)
            _bracketHighlight.SetBrushes(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.BracketBackground)) { Opacity = 0.6 },
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.Bracket)));
        if (_searchHighlight is not null)
            _searchHighlight.SetBrushes(
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.SearchMatch)) { Opacity = 0.5 },
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.SearchCurrent)) { Opacity = 0.8 },
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheme.SearchCurrent)));

        _textView.InvalidateVisual();
    }

    public void ApplyHighlighting(IHighlightingDefinition? definition)
    {
        if (ReferenceEquals(_currentDefinition, definition)) return;
        _currentDefinition = definition;
        _editor.SyntaxHighlighting = definition;
        _textView.Redraw();
    }

    public void AttachFolding(LanguageDefinition language)
    {
        if (_foldingManager is not null)
        {
            try { FoldingManager.Uninstall(_foldingManager); } catch { /* ignore */ }
            _foldingManager = null;
        }
        _xmlStrategy = null;
        _braceStrategy = null;
        _indentationStrategy = null;
        _markdownStrategy = null;

        if (!_settings.EnableFolding) return;
        if (language.Id is "plaintext") return;

        _foldingManager = FoldingManager.Install(_textArea);
        if (language.Id == "markdown")
        {
            _markdownStrategy = new MarkdownFoldingStrategy();
        }
        else if (language.Id is "xml" or "html")
        {
            _xmlStrategy = new XmlFoldingStrategy(language.Id == "html");
        }
        else if (language.FoldByIndentation)
        {
            _indentationStrategy = new IndentationFoldingStrategy(_settings.TabSize);
        }
        else
        {
            _braceStrategy = new BraceFoldingStrategy();
        }

        _foldingTimer?.Start();
    }

    public void UpdateSearchMatches(IReadOnlyList<(int Start, int Length)> matches, int currentIndex)
    {
        if (_searchHighlight is null) return;
        _searchHighlight.Matches = matches;
        _searchHighlight.CurrentIndex = currentIndex;
        _textView.InvalidateVisual();
    }

    public void ClearSearchMatches()
    {
        if (_searchHighlight is null) return;
        _searchHighlight.Matches = [];
        _searchHighlight.CurrentIndex = -1;
        _textView.InvalidateVisual();
    }

    public void RestoreView(double verticalOffset, double horizontalOffset, int caretOffset)
    {
        _suppressCaret = true;
        try
        {
            if (caretOffset >= 0 && _editor.Document.TextLength > 0)
                _editor.CaretOffset = Math.Clamp(caretOffset, 0, _editor.Document.TextLength);
            else if (_editor.Document.TextLength > 0)
                _editor.CaretOffset = 0;
        }
        finally
        {
            _suppressCaret = false;
        }

        _editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (verticalOffset > 0) _editor.ScrollToVerticalOffset(verticalOffset);
                if (horizontalOffset > 0) _editor.ScrollToHorizontalOffset(horizontalOffset);
            }
            catch { /* layout not ready yet */ }
            UpdateCaretRenderers();
        }), DispatcherPriority.Loaded);
    }

    public void CaptureFoldedOffsets(HashSet<int> offsets)
    {
        offsets.Clear();
        if (_foldingManager is null) return;
        foreach (var folding in _foldingManager.AllFoldings)
        {
            if (folding.IsFolded) offsets.Add(folding.StartOffset);
        }
    }

    public void RestoreFoldedOffsets(HashSet<int> offsets)
    {
        if (_foldingManager is null || offsets.Count == 0) return;
        _pendingFoldedOffsets = offsets;
        _foldingTimer?.Stop();
        _foldingTimer?.Start();
    }

    public void GoToLine(int line)
    {
        var target = _editor.Document.GetLineByNumber(Math.Clamp(line, 1, _editor.Document.LineCount));
        _editor.ScrollToLine(target.LineNumber);
        _editor.CaretOffset = target.Offset;
        _editor.Focus();
    }

    public void Zoom(double delta)
    {
        var newSize = Math.Clamp(_editor.FontSize + delta, 8, 48);
        if (Math.Abs(newSize - _editor.FontSize) < 0.01) return;
        _editor.FontSize = newSize;
    }

    public void ResetZoom() => _editor.FontSize = 14;

    public void ToggleOverstrike()
    {
        _editor.TextArea.OverstrikeMode = !_editor.TextArea.OverstrikeMode;
        RaiseCaretChanged();
    }

    public void EnsureFocused() => _editor.Focus();

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        UpdateCaretRenderers();
        if (!_suppressCaret) RaiseCaretChanged();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (!_suppressCaret) RaiseCaretChanged();
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        DocumentMutated?.Invoke(this, EventArgs.Empty);
        if (_foldingManager is not null && _settings.EnableFolding) _foldingTimer?.Start();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (_foldingManager is not null)
        {
            try { FoldingManager.Uninstall(_foldingManager); } catch { /* ignore */ }
            _foldingManager = null;
        }
        _foldingTimer?.Stop();
    }

    private void OnFoldingTimerTick(object? sender, EventArgs e)
    {
        _foldingTimer?.Stop();
        if (_foldingManager is null || _editor.Document is null) return;
        try
        {
            if (_xmlStrategy is not null)
                _foldingManager.UpdateFoldings(_xmlStrategy.CreateNewFoldings(_editor.Document, out _), -1);
            else if (_markdownStrategy is not null)
                _foldingManager.UpdateFoldings(_markdownStrategy.CreateNewFoldings(_editor.Document, out _), -1);
            else if (_indentationStrategy is not null)
                _foldingManager.UpdateFoldings(_indentationStrategy.CreateNewFoldings(_editor.Document, out _), -1);
            else if (_braceStrategy is not null)
                _foldingManager.UpdateFoldings(_braceStrategy.CreateNewFoldings(_editor.Document, out _), -1);

            if (_pendingFoldedOffsets is { Count: > 0 } pending)
            {
                foreach (var folding in _foldingManager.AllFoldings)
                    folding.IsFolded = pending.Contains(folding.StartOffset);
                _pendingFoldedOffsets = null;
            }
        }
        catch { /* folding must never crash the editor */ }
    }

    private void UpdateCaretRenderers()
    {
        if (_bracketHighlight is not null) _bracketHighlight.CaretOffset = _editor.CaretOffset;
        _textView.InvalidateVisual();
    }

    private void RaiseCaretChanged()
    {
        var caret = _editor.TextArea.Caret;
        var line = caret.Line;
        var column = caret.Column;
        var selection = _editor.TextArea.Selection;
        var length = selection.Length;
        var lines = 0;
        if (length > 0)
        {
            var start = _editor.Document.GetLineByOffset(selection.SurroundingSegment.Offset);
            var end = _editor.Document.GetLineByOffset(selection.SurroundingSegment.EndOffset);
            lines = Math.Max(1, end.LineNumber - start.LineNumber + 1);
        }

        CaretChanged?.Invoke(this, new CaretChangedEventArgs(
            line,
            column,
            length,
            lines,
            _editor.TextArea.OverstrikeMode));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_foldingTimer is not null)
        {
            _foldingTimer.Stop();
            _foldingTimer.Tick -= OnFoldingTimerTick;
        }
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.TextArea.SelectionChanged -= OnSelectionChanged;
        _editor.TextChanged -= OnTextChanged;
        _editor.DocumentChanged -= OnDocumentChanged;
        if (_foldingManager is not null)
            try { FoldingManager.Uninstall(_foldingManager); } catch { /* ignore */ }
    }
}

public sealed class CaretChangedEventArgs(
    int line,
    int column,
    int selectionLength,
    int selectionLineCount,
    bool overstrike) : EventArgs
{
    public int Line { get; } = line;
    public int Column { get; } = column;
    public int SelectionLength { get; } = selectionLength;
    public int SelectionLineCount { get; } = selectionLineCount;
    public bool Overstrike { get; } = overstrike;
}

