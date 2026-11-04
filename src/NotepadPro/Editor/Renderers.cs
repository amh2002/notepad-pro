using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace NotepadPro.Editor;

/// <summary>Draws subtle vertical guides at each indentation stop.</summary>
public sealed class IndentationGuideRenderer : IBackgroundRenderer
{
    private Pen? _pen;

    public KnownLayer Layer => KnownLayer.Background;
    public bool IsEnabled { get; set; } = true;
    public int IndentationSize { get; set; } = 4;

    public Brush GuideBrush
    {
        get => _pen?.Brush ?? Brushes.Transparent;
        set
        {
            var brush = value?.Clone() ?? Brushes.Transparent;
            brush.Freeze();
            _pen = new Pen(brush, 1);
            _pen.Freeze();
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || _pen is null || textView.Document is null || !textView.VisualLinesValid) return;
        var scroll = textView.ScrollOffset;
        var tabSize = Math.Max(1, IndentationSize);

        foreach (var visualLine in textView.VisualLines)
        {
            if (visualLine.FirstDocumentLine != visualLine.LastDocumentLine) continue;
            var documentLine = visualLine.FirstDocumentLine;
            var text = textView.Document.GetText(documentLine.Offset, documentLine.Length);
            if (text.Length == 0) continue;

            var columns = 0;
            foreach (var ch in text)
            {
                if (ch == ' ') columns++;
                else if (ch == '\t') columns += tabSize - (columns % tabSize);
                else break;
            }
            if (columns < tabSize) continue;

            var top = visualLine.VisualTop - scroll.Y;
            var bottom = top + visualLine.Height;
            for (var column = tabSize; column <= columns; column += tabSize)
            {
                var point = textView.GetVisualPosition(
                    new TextViewPosition(documentLine.LineNumber, column + 1),
                    VisualYPosition.LineTop);
                var x = point.X - scroll.X;
                drawingContext.DrawLine(_pen, new Point(x, top), new Point(x, bottom));
            }
        }
    }
}

/// <summary>Highlights the bracket at the caret and its partner.</summary>
public sealed class BracketHighlightRenderer : IBackgroundRenderer
{
    private static readonly HashSet<char> Openers = ['(', '[', '{'];
    private static readonly HashSet<char> Closers = [')', ']', '}'];

    private Brush _background = Brushes.Transparent;
    private Brush _border = Brushes.Transparent;

    public KnownLayer Layer => KnownLayer.Selection;
    public bool IsEnabled { get; set; } = true;
    public int CaretOffset { get; set; }

    public void SetBrushes(Brush background, Brush border)
    {
        _background = background;
        _border = border;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || textView.Document is null) return;
        var document = textView.Document;
        var text = document.Text;
        if (text.Length == 0) return;

        var index = FindBracketAt(text, CaretOffset);
        if (index < 0) return;
        var match = FindPartner(text, index);
        if (match < 0) return;

        DrawBracket(textView, drawingContext, index);
        DrawBracket(textView, drawingContext, match);
    }

    private void DrawBracket(TextView textView, DrawingContext context, int offset)
    {
        var builder = new BackgroundGeometryBuilder
        {
            AlignToWholePixels = true,
            CornerRadius = 2,
            BorderThickness = 1
        };
        builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = 1 });
        var geometry = builder.CreateGeometry();
        context.DrawGeometry(_background, new Pen(_border, 1), geometry);
    }

    private static int FindBracketAt(string text, int caretOffset)
    {
        if (caretOffset < text.Length && IsBracket(text[caretOffset])) return caretOffset;
        if (caretOffset > 0 && IsBracket(text[caretOffset - 1])) return caretOffset - 1;
        return -1;
    }

    private static bool IsBracket(char ch) => Openers.Contains(ch) || Closers.Contains(ch);

    private static int FindPartner(string text, int index)
    {
        var ch = text[index];
        if (Openers.Contains(ch))
        {
            var closing = ch switch { '(' => ')', '[' => ']', _ => '}' };
            var depth = 1;
            for (var i = index + 1; i < text.Length; i++)
            {
                if (text[i] == ch) depth++;
                else if (text[i] == closing && --depth == 0) return i;
            }
            return -1;
        }

        var opening = ch switch { ')' => '(', ']' => '[', _ => '{' };
        var depthBack = 1;
        for (var i = index - 1; i >= 0; i--)
        {
            if (text[i] == ch) depthBack++;
            else if (text[i] == opening && --depthBack == 0) return i;
        }
        return -1;
    }
}

/// <summary>Paints every search hit, with a stronger colour for the current hit.</summary>
public sealed class SearchHighlightRenderer : IBackgroundRenderer
{
    private Brush _matchBrush = Brushes.Transparent;
    private Brush _currentBrush = Brushes.Transparent;
    private Brush _currentBorder = Brushes.Transparent;

    public KnownLayer Layer => KnownLayer.Selection;
    public IReadOnlyList<(int Start, int Length)> Matches { get; set; } = [];
    public int CurrentIndex { get; set; } = -1;

    public void SetBrushes(Brush match, Brush current, Brush border)
    {
        _matchBrush = match;
        _currentBrush = current;
        _currentBorder = border;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Matches.Count == 0 || textView.Document is null || !textView.VisualLinesValid) return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;
        var firstOffset = visualLines[0].FirstDocumentLine.Offset;
        var lastLine = visualLines[^1].LastDocumentLine;
        var lastOffset = lastLine.EndOffset;

        for (var index = 0; index < Matches.Count; index++)
        {
            var (start, length) = Matches[index];
            if (start + Math.Max(1, length) < firstOffset) continue;
            if (start > lastOffset) break;

            var isCurrent = index == CurrentIndex;
            var builder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 2,
                BorderThickness = isCurrent ? 1 : 0
            };
            builder.AddSegment(textView, new TextSegment
            {
                StartOffset = Math.Clamp(start, 0, textView.Document.TextLength),
                Length = Math.Max(1, Math.Min(length, textView.Document.TextLength - start))
            });
            var geometry = builder.CreateGeometry();
            if (geometry is null) continue;

            if (isCurrent)
                drawingContext.DrawGeometry(_currentBrush, new Pen(_currentBorder, 1), geometry);
            else
                drawingContext.DrawGeometry(_matchBrush, null, geometry);
        }
    }
}

/// <summary>Highlights the full width of the current line with a choice of colour.</summary>
public sealed class CurrentLineRenderer : IBackgroundRenderer
{
    private Brush _brush = Brushes.Transparent;

    public KnownLayer Layer => KnownLayer.Background;
    public bool IsEnabled { get; set; }
    public int LineNumber { get; set; } = 1;

    public void SetBrush(Brush brush) => _brush = brush;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || textView.Document is null || !textView.VisualLinesValid) return;
        var line = textView.Document.GetLineByNumber(Math.Clamp(LineNumber, 1, textView.Document.LineCount));
        var segment = new TextSegment { StartOffset = line.Offset, Length = line.Length };
        var builder = new BackgroundGeometryBuilder
        {
            AlignToWholePixels = true,
            ExtendToFullWidthAtLineEnd = true
        };
        builder.AddSegment(textView, segment);
        var geometry = builder.CreateGeometry();
        if (geometry is not null) drawingContext.DrawGeometry(_brush, null, geometry);
    }
}

