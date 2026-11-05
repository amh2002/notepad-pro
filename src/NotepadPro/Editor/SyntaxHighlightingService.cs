using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;
using NotepadPro.Theming;

namespace NotepadPro.Editor;

/// <summary>
/// Generates AvalonEdit XSHD definitions from the declarative language catalogue and the
/// active editor colour scheme. Generated definitions are cached by language + palette.
/// </summary>
public sealed class SyntaxHighlightingService
{
    private const string XshdNamespace = "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008";
    private readonly Dictionary<string, IHighlightingDefinition> _cache = new(StringComparer.Ordinal);

    public IHighlightingDefinition Get(LanguageDefinition language, EditorColorScheme scheme)
    {
        var key = BuildKey(language, scheme);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var xml = BuildXshd(language, scheme);
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            _cache[key] = definition;
            return definition;
        }
        catch (Exception ex) when (ex is XmlException or HighlightingDefinitionInvalidException)
        {
            // A generator defect should never stop the user from editing. Fall back to plain text,
            // but leave a trace so the defect is diagnosable instead of invisible.
            Debug.WriteLine($"Failed to load highlighting for '{language.Id}': {ex}");
            var fallback = BuildPlainText(scheme);
            _cache[key] = fallback;
            return fallback;
        }
    }

    public void ClearCache() => _cache.Clear();

    private static string BuildKey(LanguageDefinition language, EditorColorScheme scheme)
    {
        var palette = string.Join(";", scheme.Syntax.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + pair.Value));
        return $"{language.Id}|{language.Keywords.Length}|{language.TypeKeywords.Length}|{language.Builtins.Length}|{scheme.Id}|{palette}";
    }

    private static IHighlightingDefinition BuildPlainText(EditorColorScheme scheme)
    {
        var xml = $"""
            <SyntaxDefinition name="Plain Text" extensions=".txt" xmlns="{XshdNamespace}">
              <Color name="foreground" foreground="{scheme.Foreground}" />
              <RuleSet />
            </SyntaxDefinition>
            """;
        using var reader = XmlReader.Create(new StringReader(xml));
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    internal static string BuildXshd(LanguageDefinition language, EditorColorScheme scheme)
    {
        var sb = new StringBuilder(16 * 1024);
        sb.Append("<SyntaxDefinition name=\"").Append(Xml(language.DisplayName))
          .Append("\" extensions=\"").Append(Xml(string.Join(",", language.Extensions)))
          .Append("\" xmlns=\"").Append(XshdNamespace).Append("\">\n");

        foreach (var category in SyntaxCategories.All)
        {
            var color = scheme.Syntax.TryGetValue(category.Key, out var value) ? value : scheme.Foreground;
            sb.Append("  <Color name=\"").Append(category.Key).Append("\" foreground=\"")
              .Append(Xml(color)).Append('"');
            if (category.Key is "comment" or "quote" or "emphasis") sb.Append(" fontStyle=\"italic\"");
            if (category.Key is "heading") sb.Append(" fontWeight=\"bold\"");
            sb.Append(" />\n");
        }

        sb.Append("  <RuleSet>\n");

        switch (language.Id)
        {
            case "plaintext":
                break;
            case "csharp":
                AddCSharp(sb);
                break;
            case "c":
            case "cpp":
                AddCLike(sb, language.Id == "cpp");
                break;
            case "java":
                AddJava(sb);
                break;
            case "javascript":
            case "typescript":
                AddJavaScript(sb, language.Id == "typescript");
                break;
            case "python":
                AddPython(sb);
                break;
            case "html":
            case "xml":
                AddMarkup(sb, language.Id == "xml");
                break;
            case "css":
            case "scss":
                AddCss(sb, language, language.Id == "scss");
                break;
            case "json":
                AddJson(sb);
                break;
            case "sql":
                AddSql(sb, language);
                return Finish(sb);
            case "php":
                AddPhp(sb);
                break;
            case "go":
                AddGo(sb);
                break;
            case "rust":
                AddRust(sb);
                break;
            case "bash":
                AddBash(sb);
                break;
            case "powershell":
                AddPowerShell(sb, language);
                return Finish(sb);
            case "yaml":
                AddYaml(sb);
                break;
            case "markdown":
                AddMarkdown(sb);
                break;
            default:
                AddGeneric(sb, language);
                break;
        }

        if (language.Id is not ("html" or "xml" or "markdown" or "json" or "yaml" or "css" or "scss"))
        {
            AddTodoRule(sb);
        }

        return Finish(sb);
    }

    private static string Finish(StringBuilder sb)
    {
        sb.Append("  </RuleSet>\n</SyntaxDefinition>\n");
        return sb.ToString();
    }

    private static void AddTodoRule(StringBuilder sb)
    {
        sb.AppendLine("    <Rule color=\"todo\">\\b(?:TODO|FIXME|HACK|XXX)\\b</Rule>");
    }

    private static void AddCommonRules(StringBuilder sb, LanguageDefinition language, bool caseInsensitive)
    {
        AddKeywords(sb, "preprocessor", ["#region", "#endregion", "#if", "#else", "#elif", "#endif", "#define", "#undef", "#include", "#pragma", "#nullable", "#error", "#warning"], caseInsensitive: false);
        AddKeywords(sb, "keyword", language.Keywords, caseInsensitive);
        AddKeywords(sb, "type", language.TypeKeywords, caseInsensitive);
        AddKeywords(sb, "constant", language.Constants, caseInsensitive);
        AddKeywords(sb, "builtin", language.Builtins, caseInsensitive);
        AddKeywords(sb, "property", language.Properties, caseInsensitive);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, caseInsensitive);
        AddRule(sb, "operator", @"[+\-*/%=<>!&|^~?:]+");
        AddRule(sb, "todo", @"\b(?:TODO|FIXME|HACK|XXX|NOTE)\b");
    }

    private static void AddCSharp(StringBuilder sb)
    {
        AddLineComment(sb, "//");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddPreprocessorLine(sb);
        AddVerbatimString(sb, '@');
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddKeywords(sb, "keyword", KeywordsOf("csharp"), false);
        AddKeywords(sb, "type", TypesOf("csharp"), false);
        AddKeywords(sb, "constant", ConstantsOf("csharp"), false);
        AddKeywords(sb, "builtin", BuiltinsOf("csharp"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
        AddRule(sb, "regex", @"(?<![\w])@?\$?""(?:[^""\\]|\\.)*""");
    }

    private static void AddCLike(StringBuilder sb, bool cpp)
    {
        AddLineComment(sb, "//");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddPreprocessorLine(sb);
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddKeywords(sb, "keyword", KeywordsOf(cpp ? "cpp" : "c"), false);
        AddKeywords(sb, "type", TypesOf(cpp ? "cpp" : "c"), false);
        AddKeywords(sb, "constant", ConstantsOf(cpp ? "cpp" : "c"), false);
        AddKeywords(sb, "builtin", BuiltinsOf(cpp ? "cpp" : "c"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
    }

    private static void AddJava(StringBuilder sb)
    {
        AddLineComment(sb, "//");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddCommentAnnotation(sb);
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddKeywords(sb, "keyword", KeywordsOf("java"), false);
        AddKeywords(sb, "type", TypesOf("java"), false);
        AddKeywords(sb, "constant", ConstantsOf("java"), false);
        AddKeywords(sb, "builtin", BuiltinsOf("java"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
    }

    private static void AddJavaScript(StringBuilder sb, bool typescript)
    {
        AddLineComment(sb, "//");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddBacktickString(sb);
        AddKeywords(sb, "keyword", KeywordsOf(typescript ? "typescript" : "javascript"), false);
        AddKeywords(sb, "type", TypesOf(typescript ? "typescript" : "javascript"), false);
        AddKeywords(sb, "constant", ConstantsOf(typescript ? "typescript" : "javascript"), false);
        AddKeywords(sb, "builtin", BuiltinsOf(typescript ? "typescript" : "javascript"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
        AddRule(sb, "regex", @"(?<![\w)])/(?![/*])(?:[^/\\\r\n]|\\.)+/[gimsuy]*");
        AddRule(sb, "variable", @"\$\{[^}]+\}|\$[A-Za-z_]\w*");
    }

    private static void AddPython(StringBuilder sb)
    {
        AddLineComment(sb, "#");
        AddTripleString(sb, "\"\"\"", '\\');
        AddTripleString(sb, "'''", '\\');
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddRule(sb, "preprocessor", @"^\s*@[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*");
        AddKeywords(sb, "keyword", KeywordsOf("python"), false);
        AddKeywords(sb, "type", TypesOf("python"), false);
        AddKeywords(sb, "constant", ConstantsOf("python"), false);
        AddKeywords(sb, "builtin", BuiltinsOf("python"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
    }

    private static void AddMarkup(StringBuilder sb, bool xml)
    {
        AddBlockComment(sb, "<!--", "-->");
        AddRule(sb, "preprocessor", @"<\?[\w:.-]+");
        AddRule(sb, "preprocessor", @"<!DOCTYPE[^>]*>");
        AddRule(sb, "escape", @"\x26(?:\x23\d+|\x23x[0-9a-fA-F]+|[A-Za-z][A-Za-z0-9]+);");

        // A tag from '<' to '>' with attribute and string sub-highlighting.
        sb.Append("    <Span color=\"tag\" begin=\"&lt;/?[A-Za-z][\\w:.-]*\" end=\"/?&gt;\" multiline=\"true\">\n");
        sb.Append("      <RuleSet>\n");
        sb.Append("        <Rule color=\"attribute\">[A-Za-z_:][-\\w:.]*(?=\\s*=)</Rule>\n");
        AddQuotedString(sb, "\"", '\\', indent: "        ");
        AddQuotedString(sb, "'", '\\', indent: "        ");
        sb.Append("        <Rule color=\"operator\">=</Rule>\n");
        sb.Append("      </RuleSet>\n");
        sb.Append("    </Span>\n");

        if (!xml)
        {
            AddKeywords(sb, "tag", TagNames(), caseInsensitive: true);
        }

        AddRule(sb, "number", @"\b\d+(?:\.\d+)?\b");
        AddRule(sb, "constant", @"\x26(?:[A-Za-z][A-Za-z0-9]+|\x23\d+);");
    }

    private static void AddCss(StringBuilder sb, LanguageDefinition language, bool scss)
    {
        AddBlockComment(sb, @"/\*", @"\*/");
        AddLineComment(sb, "//");
        if (scss)
        {
            AddRule(sb, "variable", @"\$[A-Za-z_-][\w-]*");
            AddRule(sb, "preprocessor", @"@[A-Za-z_-][\w-]*");
        }
        else
        {
            AddRule(sb, "preprocessor", @"@[A-Za-z_-][\w-]*");
        }

        AddRule(sb, "property", @"\b(?:-{0,2}[A-Za-z][\w-]*)(?=\s*:)");
        AddRule(sb, "constant", @"\x23[0-9A-Fa-f]{3,8}\b");
        AddRule(sb, "number", @"\b\d+(?:\.\d+)?(?:%|px|em|rem|vh|vw|deg|ms|s|fr)?\b");
        AddKeywords(sb, "type", language.TypeKeywords, caseInsensitive: true);
        AddKeywords(sb, "builtin", language.Builtins, caseInsensitive: true);
        AddKeywords(sb, "constant", language.Constants, caseInsensitive: true);
        AddRule(sb, "operator", @"[{}:;,>+~*=/]");
        AddRule(sb, "class", @"\.[A-Za-z_-][\w-]*");
        AddRule(sb, "tag", @"(?:^|(?<=\s))[A-Za-z][\w-]*(?=[\s,{.#:])");
    }

    private static void AddJson(StringBuilder sb)
    {
        AddLineComment(sb, "//");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddRule(sb, "property", @"""(?:[^""\\]|\\.)*""(?=\s*:)");
        AddQuotedString(sb, "\"", '\\');
        AddKeywords(sb, "constant", ["true", "false", "null"], caseInsensitive: false);
        AddNumberRule(sb);
        AddRule(sb, "operator", @"[{}\[\],:]");
    }

    private static void AddSql(StringBuilder sb, LanguageDefinition language)
    {
        AddLineComment(sb, "--");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddRule(sb, "string", @"'(?:[^']|'')*'");
        AddQuotedString(sb, "\"", '\\');
        AddKeywords(sb, "keyword", language.Keywords, caseInsensitive: true);
        AddKeywords(sb, "type", language.TypeKeywords, caseInsensitive: true);
        AddKeywords(sb, "constant", language.Constants, caseInsensitive: true);
        AddKeywords(sb, "builtin", language.Builtins, caseInsensitive: true);
        AddNumberRule(sb);
        AddRule(sb, "variable", @"[@:][A-Za-z_]\w*");
        AddRule(sb, "operator", @"[=<>!+\-*/%|&^~]+");
    }

    private static void AddPhp(StringBuilder sb)
    {
        AddLineComment(sb, "//");
        AddLineComment(sb, "#");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddRule(sb, "preprocessor", @"<\?php|<\?=|\?>");
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddRule(sb, "variable", @"\$[A-Za-z_]\w*");
        AddKeywords(sb, "keyword", KeywordsOf("php"), false);
        AddKeywords(sb, "type", TypesOf("php"), false);
        AddKeywords(sb, "constant", ConstantsOf("php"), false);
        AddKeywords(sb, "builtin", BuiltinsOf("php"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
    }

    private static void AddGo(StringBuilder sb)
    {
        AddLineComment(sb, "//");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddBacktickString(sb);
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddKeywords(sb, "keyword", KeywordsOf("go"), false);
        AddKeywords(sb, "type", TypesOf("go"), false);
        AddKeywords(sb, "constant", ConstantsOf("go"), false);
        AddKeywords(sb, "builtin", BuiltinsOf("go"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
    }

    private static void AddRust(StringBuilder sb)
    {
        AddLineComment(sb, "//");
        AddBlockComment(sb, @"/\*", @"\*/");
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddRule(sb, "preprocessor", @"\x23!?\[[^\]]*\]|\b[A-Za-z_]\w*!");
        AddKeywords(sb, "keyword", KeywordsOf("rust"), false);
        AddKeywords(sb, "type", TypesOf("rust"), false);
        AddKeywords(sb, "constant", ConstantsOf("rust"), false);
        AddKeywords(sb, "builtin", BuiltinsOf("rust"), false);
        AddNumberRule(sb);
        AddFunctionAndClassRules(sb, false);
    }

    private static void AddBash(StringBuilder sb)
    {
        AddLineComment(sb, "#");
        AddRule(sb, "preprocessor", @"^\x23!.*$");
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\\');
        AddRule(sb, "variable", @"\$\{[^}]+\}|\$[A-Za-z_]\w*|\$[0-9@*#?$!-]");
        AddKeywords(sb, "keyword", KeywordsOf("bash"), false);
        AddKeywords(sb, "type", TypesOf("bash"), false);
        AddKeywords(sb, "constant", ConstantsOf("bash"), false);
        AddKeywords(sb, "builtin", BuiltinsOf("bash"), false);
        AddNumberRule(sb);
        AddRule(sb, "operator", @"[|&;<>()$`\\]+");
    }

    private static void AddPowerShell(StringBuilder sb, LanguageDefinition language)
    {
        AddLineComment(sb, "#");
        AddBlockComment(sb, "<#", "#>");
        AddQuotedString(sb, "\"", '`');
        AddQuotedString(sb, "'", '\'');
        AddRule(sb, "variable", @"\$[A-Za-z_][\w:]*|\$\{[^}]+\}");
        AddKeywords(sb, "keyword", language.Keywords, caseInsensitive: true);
        AddKeywords(sb, "type", language.TypeKeywords, caseInsensitive: true);
        AddKeywords(sb, "constant", language.Constants, caseInsensitive: true);
        AddKeywords(sb, "builtin", language.Builtins, caseInsensitive: true);
        AddNumberRule(sb);
        AddRule(sb, "operator", @"[|&;<>()@{}\[\]=]+");
    }

    private static void AddYaml(StringBuilder sb)
    {
        AddLineComment(sb, "#");
        AddRule(sb, "property", @"^\s*(?:-\s+)?[A-Za-z_][\w.-]*(?=\s*:)");
        AddQuotedString(sb, "\"", '\\');
        AddQuotedString(sb, "'", '\'');
        AddRule(sb, "constant", @"^(?:---|\.\.\.)\s*$");
        AddKeywords(sb, "constant", ["true", "false", "null", "yes", "no", "on", "off", "~"], caseInsensitive: true);
        AddRule(sb, "variable", @"[&*][A-Za-z_][\w-]*|![A-Za-z!][\w!:.-]*");
        AddRule(sb, "number", @"\b-?\d+(?:\.\d+)?\b");
        AddRule(sb, "operator", @"[:\-?,\[\]{}|>]+");
    }

    private static void AddMarkdown(StringBuilder sb)
    {
        AddRule(sb, "heading", @"^\x23{1,6}\s.*$");
        AddRule(sb, "quote", @"^\s*>\s.*$");
        AddFencedCodeBlock(sb);
        AddRule(sb, "code", @"`[^`\r\n]+`");
        AddRule(sb, "link", @"!?\[[^\]]*\]\([^)]+\)");
        AddRule(sb, "link", @"<https?://[^>]+>");
        AddRule(sb, "link", @"https?://\S+");
        AddRule(sb, "emphasis", @"\*\*[^*\r\n]+\*\*|__[^_\r\n]+__");
        AddRule(sb, "emphasis", @"(?<![\w*])\*[^*\r\n]+\*(?![\w*])|(?<![\w_])_[^_\r\n]+_(?![\w_])");
        AddRule(sb, "keyword", @"^\s*(?:[-*+]|\d+\.)\s");
        AddRule(sb, "preprocessor", @"^\s*\|?[\s:|-]+\|?\s*$");
    }

    private static void AddGeneric(StringBuilder sb, LanguageDefinition language)
    {
        AddLineComment(sb, language.LineComment);
        if (language.BlockCommentStart is not null && language.BlockCommentEnd is not null)
            AddBlockComment(sb, language.BlockCommentStart, language.BlockCommentEnd);
        AddQuotedString(sb, "\"", '\\');
        AddKeywords(sb, "keyword", language.Keywords, language.CaseInsensitive);
        AddKeywords(sb, "type", language.TypeKeywords, language.CaseInsensitive);
        AddKeywords(sb, "constant", language.Constants, language.CaseInsensitive);
        AddKeywords(sb, "builtin", language.Builtins, language.CaseInsensitive);
    }

    // ---------------------------------------------------------------- helpers

    private static void AddLineComment(StringBuilder sb, string? token)
    {
        if (string.IsNullOrEmpty(token)) return;
        sb.Append("    <Span color=\"comment\" begin=\"").Append(Xml(Regex.Escape(token))).AppendLine("\" />");
    }

    private static void AddBlockComment(StringBuilder sb, string begin, string end)
    {
        sb.Append("    <Span color=\"comment\" begin=\"").Append(Xml(Regex.Escape(begin)))
          .Append("\" end=\"").Append(Xml(Regex.Escape(end))).AppendLine("\" multiline=\"true\" />");
    }

    private static void AddPreprocessorLine(StringBuilder sb)
    {
        sb.AppendLine("    <Rule color=\"preprocessor\">^\\s*\\x23\\s*\\w+.*$</Rule>");
    }

    private static void AddCommentAnnotation(StringBuilder sb)
    {
        sb.AppendLine("    <Rule color=\"preprocessor\">@[A-Za-z_]\\w*</Rule>");
    }

    private static void AddQuotedString(StringBuilder sb, string delimiter, char escapeChar, string indent = "    ")
    {
        sb.Append(indent).Append("<Span color=\"string\" begin=\"").Append(Xml(Regex.Escape(delimiter)))
          .Append("\" end=\"").Append(Xml(Regex.Escape(delimiter))).AppendLine("\" multiline=\"true\">");
        sb.Append(indent).AppendLine("  <RuleSet>");
        if (escapeChar == '\'')
        {
            sb.Append(indent).AppendLine("    <Span color=\"escape\" begin=\"''\" end=\".\" />");
        }
        else
        {
            sb.Append(indent).Append("    <Span color=\"escape\" begin=\"").Append(Xml(Regex.Escape("\\")))
              .AppendLine("\" end=\".\" />");
            if (delimiter == "\"")
            {
                // Keep string interpolation fragments readable in template-like languages.
                sb.Append(indent).AppendLine("    <Rule color=\"variable\">\\$\\{[^}]*\\}</Rule>");
            }
        }
        sb.Append(indent).AppendLine("  </RuleSet>");
        sb.Append(indent).AppendLine("</Span>");
    }

    private static void AddVerbatimString(StringBuilder sb, char prefix)
    {
        sb.Append("    <Span color=\"string\" begin=\"").Append(Xml(Regex.Escape(prefix + "\"")))
          .Append("\" end=\"").Append(Xml(Regex.Escape("\""))).AppendLine("\" multiline=\"true\">");
        sb.AppendLine("      <RuleSet>");
        sb.AppendLine("        <Span color=\"escape\" begin=\"&quot;&quot;\" end=\".\" />");
        sb.AppendLine("      </RuleSet>");
        sb.AppendLine("    </Span>");
    }

    private static void AddTripleString(StringBuilder sb, string delimiter, char escapeChar)
    {
        sb.Append("    <Span color=\"string\" begin=\"").Append(Xml(Regex.Escape(delimiter)))
          .Append("\" end=\"").Append(Xml(Regex.Escape(delimiter))).AppendLine("\" multiline=\"true\">");
        sb.AppendLine("      <RuleSet>");
        sb.Append("        <Span color=\"escape\" begin=\"").Append(Xml(Regex.Escape(escapeChar.ToString())))
          .AppendLine("\" end=\".\" />");
        sb.AppendLine("      </RuleSet>");
        sb.AppendLine("    </Span>");
    }

    private static void AddBacktickString(StringBuilder sb)
    {
        sb.AppendLine("    <Span color=\"string\" begin=\"`\" end=\"`\" multiline=\"true\">");
        sb.AppendLine("      <RuleSet>");
        sb.AppendLine("        <Span color=\"escape\" begin=\"\\\\\" end=\".\" />");
        sb.AppendLine("        <Rule color=\"variable\">\\$\\{[^}]*\\}</Rule>");
        sb.AppendLine("      </RuleSet>");
        sb.AppendLine("    </Span>");
    }

    private static void AddFencedCodeBlock(StringBuilder sb)
    {
        sb.AppendLine("    <Span color=\"code\" begin=\"^```.*$\" end=\"^```\\s*$\" multiline=\"true\" />");
    }

    private static void AddKeywords(StringBuilder sb, string category, IEnumerable<string> words,
        bool caseInsensitive)
    {
        var escaped = words
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(word => word.Length)
            .Select(Regex.Escape)
            .ToList();
        if (escaped.Count == 0) return;

        var body = $@"(?<!\w)(?:{string.Join("|", escaped)})(?!\w)";
        if (caseInsensitive) body = $"(?i:{body})";
        sb.Append("    <Rule color=\"").Append(category).Append("\">").Append(Xml(body)).AppendLine("</Rule>");
    }

    private static void AddNumberRule(StringBuilder sb)
    {
        sb.AppendLine("    <Rule color=\"number\">\\b(?:0[xX][0-9a-fA-F_]+|0[bB][01_]+|\\d[\\d_]*(?:\\.\\d[\\d_]*)?(?:[eE][+-]?\\d+)?)[uUlLfFdDmM]*\\b</Rule>");
    }

    private static void AddFunctionAndClassRules(StringBuilder sb, bool caseInsensitive)
    {
        sb.AppendLine("    <Rule color=\"function\">\\b[A-Za-z_][\\w]*(?=\\s*\\()</Rule>");
        sb.AppendLine("    <Rule color=\"class\">\\b[A-Z][A-Za-z0-9_]*\\b</Rule>");
    }

    private static void AddRule(StringBuilder sb, string category, string pattern, bool multiline = false)
    {
        sb.Append("    <Rule color=\"").Append(category).Append("\">").Append(Xml(pattern)).AppendLine("</Rule>");
    }

    private static string[] KeywordsOf(string id) => LanguageCatalog.GetById(id).Keywords;
    private static string[] TypesOf(string id) => LanguageCatalog.GetById(id).TypeKeywords;
    private static string[] ConstantsOf(string id) => LanguageCatalog.GetById(id).Constants;
    private static string[] BuiltinsOf(string id) => LanguageCatalog.GetById(id).Builtins;

    private static string[] TagNames() => LanguageCatalog.GetById("html").Tags;

    private static string Xml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}

