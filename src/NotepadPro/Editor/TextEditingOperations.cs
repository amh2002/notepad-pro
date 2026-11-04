using System.Text;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;

namespace NotepadPro.Editor;

/// <summary>Line and selection transformations exposed by the Edit and Format menus.</summary>
public static class TextEditingOperations
{
    public static void DuplicateLines(TextEditor editor)
    {
        var document = editor.Document;
        var (startLine, endLine) = GetSelectedLineRange(editor);
        var start = document.GetLineByNumber(startLine);
        var end = document.GetLineByNumber(endLine);
        var blockEnd = end.EndOffset + end.DelimiterLength;
        var block = document.GetText(start.Offset, blockEnd - start.Offset);
        var needsLeadingBreak = block.Length == 0 || block[^1] != '\n';

        using (document.RunUpdate())
        {
            document.Insert(blockEnd, block.Length == 0 ? "\n" : needsLeadingBreak ? "\n" + block : block);
        }

        var newStart = blockEnd + (needsLeadingBreak ? 1 : 0);
        SelectLines(editor, document.GetLineByOffset(newStart).LineNumber,
            document.GetLineByOffset(Math.Min(document.TextLength, newStart + block.Length - 1)).LineNumber);
    }

    public static void DeleteLines(TextEditor editor)
    {
        var document = editor.Document;
        var (startLine, endLine) = GetSelectedLineRange(editor);
        var start = document.GetLineByNumber(startLine);
        var end = document.GetLineByNumber(endLine);

        var deleteStart = start.Offset;
        var deleteLength = end.EndOffset + end.DelimiterLength - deleteStart;
        if (deleteLength <= 0) return;

        using (document.RunUpdate())
        {
            document.Remove(deleteStart, deleteLength);
        }

        var caret = Math.Clamp(deleteStart, 0, document.TextLength);
        editor.CaretOffset = caret;
        editor.TextArea.Caret.BringCaretToView();
    }

    public static bool MoveLinesUp(TextEditor editor)
    {
        var document = editor.Document;
        var (startLine, endLine) = GetSelectedLineRange(editor);
        if (startLine <= 1) return false;

        var start = document.GetLineByNumber(startLine);
        var end = document.GetLineByNumber(endLine);
        var previous = document.GetLineByNumber(startLine - 1);
        var blockEnd = end.EndOffset + end.DelimiterLength;
        var block = document.GetText(start.Offset, blockEnd - start.Offset);
        var previousBlock = document.GetText(previous.Offset, start.Offset - previous.Offset);

        string replacement;
        if (block.EndsWith('\n'))
            replacement = block + previousBlock;
        else
            replacement = block + "\n" + previousBlock.TrimEnd('\n');

        using (document.RunUpdate())
        {
            document.Replace(previous.Offset, blockEnd - previous.Offset, replacement);
        }

        SelectLines(editor, startLine - 1, endLine - 1);
        return true;
    }

    public static bool MoveLinesDown(TextEditor editor)
    {
        var document = editor.Document;
        var (startLine, endLine) = GetSelectedLineRange(editor);
        if (endLine >= document.LineCount) return false;

        var start = document.GetLineByNumber(startLine);
        var end = document.GetLineByNumber(endLine);
        var next = document.GetLineByNumber(endLine + 1);
        var nextEnd = next.EndOffset + next.DelimiterLength;
        var block = document.GetText(start.Offset, end.EndOffset + end.DelimiterLength - start.Offset);
        var nextBlock = document.GetText(next.Offset, nextEnd - next.Offset);

        string replacement;
        if (nextBlock.EndsWith('\n'))
            replacement = nextBlock + block;
        else
            replacement = nextBlock.TrimEnd('\n') + "\n" + block.TrimEnd('\n');

        using (document.RunUpdate())
        {
            document.Replace(start.Offset, nextEnd - start.Offset, replacement);
        }

        SelectLines(editor, startLine + 1, endLine + 1);
        return true;
    }

    public static bool ToggleComment(TextEditor editor, LanguageDefinition language, out string? message)
    {
        message = null;
        var document = editor.Document;
        var (startLine, endLine) = GetSelectedLineRange(editor);

        if (!string.IsNullOrEmpty(language.LineComment))
        {
            ToggleLineComments(editor, language.LineComment, startLine, endLine);
            return true;
        }

        if (!string.IsNullOrEmpty(language.BlockCommentStart) &&
            !string.IsNullOrEmpty(language.BlockCommentEnd))
        {
            ToggleBlockComment(editor, language.BlockCommentStart, language.BlockCommentEnd);
            return true;
        }

        message = $"{language.DisplayName} does not define comment syntax.";
        return false;
    }

    private static void ToggleLineComments(TextEditor editor, string token, int startLine, int endLine)
    {
        var document = editor.Document;
        var lines = Enumerable.Range(startLine, Math.Max(1, endLine - startLine + 1))
            .Select(number => document.GetLineByNumber(number))
            .Where(line => !string.IsNullOrWhiteSpace(document.GetText(line.Offset, line.Length)))
            .ToList();
        if (lines.Count == 0) return;

        var allCommented = lines.All(line =>
        {
            var text = document.GetText(line.Offset, line.Length);
            return text.TrimStart(' ', '\t').StartsWith(token, StringComparison.Ordinal);
        });

        using (document.RunUpdate())
        {
            foreach (var line in lines)
            {
                var text = document.GetText(line.Offset, line.Length);
                var indentLength = 0;
                while (indentLength < text.Length && (text[indentLength] == ' ' || text[indentLength] == '\t'))
                    indentLength++;

                if (allCommented)
                {
                    var afterIndent = text[indentLength..];
                    var removeLength = token.Length;
                    if (afterIndent.Length > token.Length && afterIndent[token.Length] == ' ')
                        removeLength++;
                    document.Remove(line.Offset + indentLength, removeLength);
                }
                else
                {
                    document.Insert(line.Offset + indentLength, token + " ");
                }
            }
        }
    }

    private static void ToggleBlockComment(TextEditor editor, string startToken, string endToken)
    {
        var document = editor.Document;
        var selection = editor.TextArea.Selection;
        if (!selection.IsEmpty)
        {
            var segment = selection.SurroundingSegment;
            var selected = document.GetText(segment.Offset, segment.Length);
            using (document.RunUpdate())
            {
                if (selected.StartsWith(startToken, StringComparison.Ordinal) &&
                    selected.EndsWith(endToken, StringComparison.Ordinal) &&
                    selected.Length >= startToken.Length + endToken.Length)
                {
                    var inner = selected[startToken.Length..^endToken.Length].Trim();
                    document.Replace(segment.Offset, segment.Length, inner);
                    editor.Select(segment.Offset, inner.Length);
                }
                else
                {
                    document.Insert(segment.Offset, startToken + " ");
                    document.Insert(segment.Offset + segment.Length + startToken.Length + 1, " " + endToken);
                    editor.Select(segment.Offset, segment.Length + startToken.Length + endToken.Length + 2);
                }
            }
            return;
        }

        var line = document.GetLineByOffset(editor.CaretOffset);
        var text = document.GetText(line.Offset, line.Length);
        var indentLength = 0;
        while (indentLength < text.Length && (text[indentLength] == ' ' || text[indentLength] == '\t'))
            indentLength++;
        var body = text[indentLength..];

        using (document.RunUpdate())
        {
            if (body.StartsWith(startToken, StringComparison.Ordinal) &&
                body.EndsWith(endToken, StringComparison.Ordinal) &&
                body.Length >= startToken.Length + endToken.Length)
            {
                var inner = body[startToken.Length..^endToken.Length].Trim();
                document.Replace(line.Offset + indentLength, body.Length, inner);
                editor.CaretOffset = line.Offset + indentLength + Math.Min(inner.Length, Math.Max(0, editor.CaretOffset - line.Offset - indentLength - startToken.Length));
            }
            else
            {
                document.Insert(line.Offset + indentLength, startToken + " ");
                document.Insert(line.Offset + indentLength + startToken.Length + 1 + body.Length, " " + endToken);
            }
        }
    }

    public static bool TrimTrailingWhitespace(TextEditor editor)
    {
        var document = editor.Document;
        var original = document.Text;
        var trimmed = FileService.TrimTrailingWhitespace(original)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        if (trimmed == original) return false;

        var caret = editor.CaretOffset;
        using (document.RunUpdate())
        {
            document.Text = trimmed;
        }
        editor.CaretOffset = Math.Clamp(caret, 0, document.TextLength);
        return true;
    }

    public static bool ConvertCase(TextEditor editor, CaseConversion conversion, out string? message)
    {
        message = null;
        var selection = editor.TextArea.Selection;
        if (selection.IsEmpty)
        {
            message = "Select some text to convert its case.";
            return false;
        }

        var document = editor.Document;
        var segment = selection.SurroundingSegment;
        var text = document.GetText(segment.Offset, segment.Length);
        var converted = conversion switch
        {
            CaseConversion.Upper => text.ToUpperInvariant(),
            CaseConversion.Lower => text.ToLowerInvariant(),
            CaseConversion.Title => ToTitleCase(text),
            CaseConversion.Sentence => ToSentenceCase(text),
            _ => text
        };

        if (converted == text) return true;
        using (document.RunUpdate())
        {
            document.Replace(segment.Offset, segment.Length, converted);
        }
        editor.Select(segment.Offset, converted.Length);
        return true;
    }

    public static void InsertDateTime(TextEditor editor)
    {
        var text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var offset = editor.CaretOffset;
        using (editor.Document.RunUpdate())
        {
            editor.Document.Insert(offset, text);
        }
        editor.CaretOffset = offset + text.Length;
    }

    public static void IndentSelection(TextEditor editor, int tabSize, bool useTabs)
    {
        var document = editor.Document;
        var (startLine, endLine) = GetSelectedLineRange(editor);
        var unit = useTabs ? "\t" : new string(' ', Math.Max(1, tabSize));
        var startSelection = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;

        using (document.RunUpdate())
        {
            for (var number = startLine; number <= endLine; number++)
            {
                var line = document.GetLineByNumber(number);
                if (line.Length == 0) continue;
                document.Insert(line.Offset, unit);
            }
        }

        editor.Select(Math.Min(document.TextLength, startSelection + unit.Length),
            Math.Max(0, selectionLength + unit.Length * Math.Max(1, endLine - startLine + 1)));
    }

    public static void OutdentSelection(TextEditor editor, int tabSize)
    {
        var document = editor.Document;
        var (startLine, endLine) = GetSelectedLineRange(editor);
        var startSelection = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        var removedTotal = 0;

        using (document.RunUpdate())
        {
            for (var number = startLine; number <= endLine; number++)
            {
                var line = document.GetLineByNumber(number);
                if (line.Length == 0) continue;
                var text = document.GetText(line.Offset, Math.Min(line.Length, tabSize));
                var remove = 0;
                if (text.StartsWith('\t')) remove = 1;
                else
                {
                    while (remove < text.Length && remove < tabSize && text[remove] == ' ') remove++;
                }
                if (remove == 0) continue;
                document.Remove(line.Offset, remove);
                removedTotal += remove;
            }
        }

        editor.Select(Math.Max(0, startSelection - (startSelection > 0 ? 1 : 0)),
            Math.Max(0, selectionLength - removedTotal));
    }

    public static (int StartLine, int EndLine) GetSelectedLineRange(TextEditor editor)
    {
        var document = editor.Document;
        var selection = editor.TextArea.Selection;
        if (selection.IsEmpty)
        {
            var line = document.GetLineByOffset(editor.CaretOffset);
            return (line.LineNumber, line.LineNumber);
        }

        var segment = selection.SurroundingSegment;
        var start = document.GetLineByOffset(segment.Offset).LineNumber;
        var end = document.GetLineByOffset(segment.EndOffset).LineNumber;
        var endLine = document.GetLineByNumber(end);
        if (segment.EndOffset == endLine.Offset && end > start) end--;
        return (start, end);
    }

    private static void SelectLines(TextEditor editor, int startLine, int endLine)
    {
        var document = editor.Document;
        var start = document.GetLineByNumber(Math.Clamp(startLine, 1, document.LineCount));
        var end = document.GetLineByNumber(Math.Clamp(endLine, 1, document.LineCount));
        var length = end.EndOffset - start.Offset;
        editor.Select(start.Offset, Math.Max(0, length));
        editor.CaretOffset = start.Offset;
        editor.TextArea.Caret.BringCaretToView();
    }

    private static string ToTitleCase(string text)
    {
        var chars = text.ToLowerInvariant().ToCharArray();
        var newWord = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                if (newWord) chars[i] = char.ToUpperInvariant(chars[i]);
                newWord = false;
            }
            else if (char.IsWhiteSpace(chars[i]))
            {
                newWord = true;
            }
        }
        return new string(chars);
    }

    private static string ToSentenceCase(string text)
    {
        var builder = new StringBuilder(text.Length);
        var capitalize = true;
        foreach (var ch in text.ToLowerInvariant())
        {
            if (capitalize && char.IsLetter(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
                capitalize = false;
            }
            else
            {
                builder.Append(ch);
            }
            if (ch is '.' or '!' or '?' or '\n') capitalize = true;
        }
        return builder.ToString();
    }
}

public enum CaseConversion
{
    Upper,
    Lower,
    Title,
    Sentence
}
