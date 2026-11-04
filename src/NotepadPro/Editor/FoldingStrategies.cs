using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace NotepadPro.Editor;

/// <summary>Folds brace-delimited blocks while ignoring braces inside strings and comments.</summary>
public sealed class BraceFoldingStrategy
{
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var text = document.Text;
        var stack = new Stack<int>();
        var result = new List<NewFolding>();
        var inLineComment = false;
        var inBlockComment = false;
        var quote = '\0';

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (inLineComment)
            {
                if (ch == '\n') inLineComment = false;
                continue;
            }
            if (inBlockComment)
            {
                if (ch == '*' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }
            if (quote != '\0')
            {
                if (ch == '\\')
                {
                    i++;
                    continue;
                }
                if (ch == quote) quote = '\0';
                continue;
            }

            if (ch == '/' && i + 1 < text.Length)
            {
                if (text[i + 1] == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }
                if (text[i + 1] == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }
            }
            if (ch is '"' or '\'' or '`')
            {
                quote = ch;
                continue;
            }

            if (ch == '{')
            {
                stack.Push(i);
            }
            else if (ch == '}')
            {
                if (stack.Count == 0)
                {
                    if (firstErrorOffset < 0) firstErrorOffset = i;
                    continue;
                }
                var start = stack.Pop();
                var startLine = document.GetLineByOffset(start);
                var endLine = document.GetLineByOffset(i);
                if (endLine.LineNumber > startLine.LineNumber)
                {
                    result.Add(new NewFolding(startLine.Offset, endLine.EndOffset)
                    {
                        Name = $"{{…}} ({endLine.LineNumber - startLine.LineNumber + 1} lines)"
                    });
                }
            }
        }

        if (stack.Count > 0 && firstErrorOffset < 0) firstErrorOffset = stack.Peek();
        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return result;
    }
}

/// <summary>Folds XML/HTML elements by matching start and end tags.</summary>
public sealed class XmlFoldingStrategy(bool html = false)
{
    private static readonly HashSet<string> HtmlVoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param",
        "source", "track", "wbr"
    };

    private static readonly Regex TagRegex = new(
        @"<(?<close>/)?(?<name>[A-Za-z_][\w:.-]*)(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return [];

        var stack = new Stack<(string Name, int Offset)>();
        var result = new List<NewFolding>();

        for (var index = 0; index < text.Length;)
        {
            var commentStart = text.IndexOf("<!--", index, StringComparison.Ordinal);
            if (commentStart < 0) commentStart = text.Length;
            var nextTag = text.IndexOf('<', index);
            if (nextTag < 0) break;

            if (commentStart < nextTag)
            {
                var commentEnd = text.IndexOf("-->", commentStart + 4, StringComparison.Ordinal);
                index = commentEnd < 0 ? text.Length : commentEnd + 3;
                continue;
            }

            var match = TagRegex.Match(text, nextTag);
            if (!match.Success || match.Index != nextTag)
            {
                index = nextTag + 1;
                continue;
            }

            index = match.Index + match.Length;
            var name = match.Groups["name"].Value;
            var attrs = match.Groups["attrs"].Value;
            if (match.Value.StartsWith("<!", StringComparison.Ordinal) ||
                match.Value.StartsWith("<?", StringComparison.Ordinal))
                continue;
            if (html && HtmlVoidElements.Contains(name)) continue;

            if (match.Groups["close"].Success)
            {
                if (stack.Count == 0) continue;
                var top = stack.Peek();
                if (!top.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    // Recover from malformed markup: search a few levels down.
                    var found = false;
                    foreach (var candidate in stack)
                    {
                        if (!candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                        found = true;
                        break;
                    }
                    if (!found) continue;
                    while (stack.Count > 0)
                    {
                        var current = stack.Pop();
                        if (current.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) break;
                    }
                }
                else
                {
                    stack.Pop();
                    var startLine = document.GetLineByOffset(top.Offset);
                    var endLine = document.GetLineByOffset(match.Index);
                    if (endLine.LineNumber > startLine.LineNumber)
                    {
                        result.Add(new NewFolding(startLine.Offset, endLine.EndOffset)
                        {
                            Name = $"<{name}>…</{name}>"
                        });
                    }
                }
            }
            else if (!attrs.TrimEnd().EndsWith('/'))
            {
                stack.Push((name, match.Index));
            }
        }

        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return result;
    }
}

/// <summary>Folds Python/YAML/Bash style blocks using indentation levels.</summary>
public sealed class IndentationFoldingStrategy(int tabSize = 4)
{
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var result = new List<NewFolding>();
        var stack = new Stack<(int Indent, int Line)>();
        var tabs = Math.Max(1, tabSize);

        for (var lineNumber = 1; lineNumber <= document.LineCount; lineNumber++)
        {
            var line = document.GetLineByNumber(lineNumber);
            var text = document.GetText(line.Offset, line.Length);
            if (string.IsNullOrWhiteSpace(text)) continue;

            var indent = 0;
            foreach (var ch in text)
            {
                if (ch == ' ') indent++;
                else if (ch == '\t') indent += tabs - (indent % tabs);
                else break;
            }

            while (stack.Count > 0 && indent <= stack.Peek().Indent)
            {
                var (startIndent, startLine) = stack.Pop();
                var endLine = lineNumber - 1;
                if (endLine > startLine)
                {
                    var start = document.GetLineByNumber(startLine);
                    var end = document.GetLineByNumber(endLine);
                    result.Add(new NewFolding(start.Offset, end.EndOffset)
                    {
                        Name = $"{startIndent}→{indent} (…)"
                    });
                }
            }

            stack.Push((indent, lineNumber));
        }

        while (stack.Count > 0)
        {
            var (startIndent, startLineNumber) = stack.Pop();
            var endLineNumber = document.LineCount;
            while (endLineNumber > startLineNumber && string.IsNullOrWhiteSpace(
                       document.GetText(document.GetLineByNumber(endLineNumber).Offset,
                           document.GetLineByNumber(endLineNumber).Length)))
                endLineNumber--;

            if (endLineNumber <= startLineNumber) continue;
            var startLine = document.GetLineByNumber(startLineNumber);
            var endLine = document.GetLineByNumber(endLineNumber);
            result.Add(new NewFolding(startLine.Offset, endLine.EndOffset)
            {
                Name = $"indent {startIndent} (…)"
            });
        }

        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return result;
    }
}

/// <summary>Folds Markdown sections by heading level.</summary>
public sealed class MarkdownFoldingStrategy
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+\S", RegexOptions.Compiled);

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var result = new List<NewFolding>();
        var stack = new Stack<(int Level, int Line)>();

        for (var lineNumber = 1; lineNumber <= document.LineCount; lineNumber++)
        {
            var line = document.GetLineByNumber(lineNumber);
            var text = document.GetText(line.Offset, line.Length);
            var match = HeadingRegex.Match(text);
            if (!match.Success) continue;
            var level = match.Groups[1].Value.Length;

            while (stack.Count > 0 && stack.Peek().Level >= level)
            {
                var (startLevel, startLineNumber) = stack.Pop();
                var endLineNumber = lineNumber - 1;
                if (endLineNumber > startLineNumber)
                {
                    var startLine = document.GetLineByNumber(startLineNumber);
                    var endLine = document.GetLineByNumber(endLineNumber);
                    result.Add(new NewFolding(startLine.Offset, endLine.EndOffset)
                    {
                        Name = $"{new string('#', startLevel)} section"
                    });
                }
            }
            stack.Push((level, lineNumber));
        }

        while (stack.Count > 0)
        {
            var (startLevel, startLineNumber) = stack.Pop();
            var endLineNumber = document.LineCount;
            if (endLineNumber > startLineNumber)
            {
                var startLine = document.GetLineByNumber(startLineNumber);
                var endLine = document.GetLineByNumber(endLineNumber);
                result.Add(new NewFolding(startLine.Offset, endLine.EndOffset)
                {
                    Name = $"{new string('#', startLevel)} section"
                });
            }
        }

        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return result;
    }
}
