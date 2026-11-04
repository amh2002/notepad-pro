using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Indentation;

namespace NotepadPro.Editor;

/// <summary>
/// Copies indentation from the previous line, adds a level after an opening construct and
/// removes a level before a closing construct. Works for brace languages, Python and YAML.
/// </summary>
public sealed class SmartIndentationStrategy : IIndentationStrategy
{
    private readonly Func<bool> _autoIndent;
    private readonly Func<int> _indentSize;
    private readonly Func<bool> _useTabs;

    public SmartIndentationStrategy(Func<bool> autoIndent, Func<int> indentSize, Func<bool> useTabs)
    {
        _autoIndent = autoIndent;
        _indentSize = indentSize;
        _useTabs = useTabs;
    }

    public void IndentLine(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        if (!_autoIndent()) return;

        var previous = FindPreviousContentLine(document, line);
        if (previous is null) return;

        var previousText = document.GetText(previous.Offset, previous.Length);
        var baseIndent = GetLeadingIndent(previousText);
        var previousTrimmed = previousText.TrimEnd();
        var currentText = document.GetText(line.Offset, line.Length);
        var currentContent = currentText.TrimStart(' ', '\t');

        var indentColumns = CountColumns(baseIndent, _indentSize());
        if (ShouldIncrease(previousTrimmed)) indentColumns += _indentSize();
        if (ShouldDecrease(currentContent)) indentColumns = Math.Max(0, indentColumns - _indentSize());

        var indent = BuildIndent(indentColumns, _indentSize(), _useTabs());
        var replacement = indent + currentContent;
        if (replacement == currentText) return;

        document.Replace(line.Offset, line.Length, replacement);
    }

    public void IndentLines(TextDocument document, int beginLine, int endLine)
    {
        ArgumentNullException.ThrowIfNull(document);
        var start = Math.Max(1, beginLine);
        var end = Math.Min(document.LineCount, endLine);
        var unit = _useTabs() ? "\t" : new string(' ', _indentSize());

        using (document.RunUpdate())
        {
            for (var number = start; number <= end; number++)
            {
                var line = document.GetLineByNumber(number);
                if (line.Length == 0) continue;
                var text = document.GetText(line.Offset, line.Length);
                if (string.IsNullOrWhiteSpace(text)) continue;
                document.Insert(line.Offset, unit);
            }
        }
    }

    private static DocumentLine? FindPreviousContentLine(TextDocument document, DocumentLine line)
    {
        var previous = line.PreviousLine;
        while (previous is not null)
        {
            var text = document.GetText(previous.Offset, previous.Length);
            if (!string.IsNullOrWhiteSpace(text)) return previous;
            previous = previous.PreviousLine;
        }
        return null;
    }

    private static string GetLeadingIndent(string text)
    {
        var index = 0;
        while (index < text.Length && (text[index] == ' ' || text[index] == '\t')) index++;
        return text[..index];
    }

    private static int CountColumns(string indent, int tabSize)
    {
        var columns = 0;
        foreach (var ch in indent)
            columns += ch == '\t' ? tabSize - (columns % tabSize) : 1;
        return columns;
    }

    private static string BuildIndent(int columns, int tabSize, bool useTabs)
    {
        if (columns <= 0) return string.Empty;
        if (useTabs)
        {
            var tabs = columns / tabSize;
            var spaces = columns % tabSize;
            return new string('\t', tabs) + new string(' ', spaces);
        }
        return new string(' ', columns);
    }

    private static bool ShouldIncrease(string previousTrimmed)
    {
        if (previousTrimmed.Length == 0) return false;
        var last = previousTrimmed[^1];
        if (last is '{' or '[' or '(' or ':' or '=' or '>') return true;
        return previousTrimmed.EndsWith("=>", StringComparison.Ordinal) ||
               previousTrimmed.EndsWith("then", StringComparison.OrdinalIgnoreCase) ||
               previousTrimmed.EndsWith("do", StringComparison.OrdinalIgnoreCase) ||
               previousTrimmed.EndsWith("try", StringComparison.OrdinalIgnoreCase) ||
               previousTrimmed.EndsWith("finally", StringComparison.OrdinalIgnoreCase) ||
               previousTrimmed.EndsWith("else", StringComparison.OrdinalIgnoreCase) ||
               previousTrimmed.EndsWith("begin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldDecrease(string currentContent)
    {
        if (currentContent.Length == 0) return false;
        return currentContent[0] is '}' or ']' or ')' ||
               StartsWithWord(currentContent, "else") ||
               StartsWithWord(currentContent, "elif") ||
               StartsWithWord(currentContent, "except") ||
               StartsWithWord(currentContent, "finally") ||
               StartsWithWord(currentContent, "catch") ||
               StartsWithWord(currentContent, "case") ||
               StartsWithWord(currentContent, "default") ||
               StartsWithWord(currentContent, "end") ||
               StartsWithWord(currentContent, "fi") ||
               StartsWithWord(currentContent, "done") ||
               StartsWithWord(currentContent, "esac");
    }

    private static bool StartsWithWord(string text, string word)
    {
        if (!text.StartsWith(word, StringComparison.OrdinalIgnoreCase)) return false;
        return text.Length == word.Length || !char.IsLetterOrDigit(text[word.Length]) && text[word.Length] != '_';
    }
}
