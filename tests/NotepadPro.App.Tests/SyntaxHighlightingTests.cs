using System.Xml.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using NotepadPro.Core.Services;
using NotepadPro.Editor;
using NotepadPro.Theming;

namespace NotepadPro.App.Tests;

/// <summary>
/// Guards the runtime XSHD generator. A malformed definition degrades silently to plain
/// text, so every language and palette must be loaded and inspected here. AvalonEdit
/// compiles rule patterns with RegexOptions.IgnorePatternWhitespace, which means an
/// unescaped '#' starts a comment and can silently empty a rule.
/// </summary>
public sealed class SyntaxHighlightingTests
{
    private static readonly XNamespace Xshd = "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008";

    public static IEnumerable<object[]> Languages() =>
        LanguageCatalog.All
            .Where(language => !language.IsPlainText)
            .Select(language => new object[] { language.Id });

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_builds_a_loadable_definition(string languageId)
    {
        var language = LanguageCatalog.GetById(languageId);

        foreach (var scheme in ColorSchemes.All)
        {
            var service = new SyntaxHighlightingService();
            var definition = service.Get(language, scheme);

            Assert.Equal(language.DisplayName, definition.Name);
            Assert.NotEmpty(definition.MainRuleSet.Rules);
            Assert.NotNull(definition.GetNamedColor("keyword"));
            Assert.NotNull(definition.GetNamedColor("comment"));
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_generated_pattern_survives_IgnorePatternWhitespace(string languageId)
    {
        var language = LanguageCatalog.GetById(languageId);
        var scheme = ColorSchemes.All.Single(s => s.Id == "vs-dark");
        var document = XDocument.Parse(SyntaxHighlightingService.BuildXshd(language, scheme));

        foreach (var element in document.Descendants())
        {
            if (element.Name == Xshd + "Rule")
            {
                var pattern = element.Value;
                Assert.False(string.IsNullOrWhiteSpace(pattern), $"{languageId}: empty rule");
                Assert.False(HasUnescapedHash(pattern), $"{languageId}: unescaped # in [{pattern}]");
                AssertCompiles(languageId, pattern);
            }
            else if (element.Name == Xshd + "Span")
            {
                foreach (var attribute in new[] { "begin", "end" })
                {
                    var pattern = (string?)element.Attribute(attribute);
                    if (pattern is not null) AssertCompiles(languageId, pattern);
                }
            }
        }
    }

    [Fact]
    public void CSharp_definition_exposes_the_palette_colors()
    {
        var service = new SyntaxHighlightingService();
        var scheme = ColorSchemes.All.Single(s => s.Id == "vs-dark");
        var definition = service.Get(LanguageCatalog.GetById("csharp"), scheme);

        Assert.Equal("C#", definition.Name);
        Assert.Equal("#FF6A9955", definition.GetNamedColor("comment")!.Foreground!.ToString());
        Assert.Equal("#FF569CD6", definition.GetNamedColor("keyword")!.Foreground!.ToString());
        Assert.Equal("#FFCE9178", definition.GetNamedColor("string")!.Foreground!.ToString());
    }

    [Fact]
    public void CSharp_tokens_are_actually_colored()
    {
        var sections = Highlight("csharp", "public sealed class Greeter\n{\n    // comment here\n    string s = \"hello\";\n}");

        Assert.Contains(sections, s => s.Color == "keyword" && s.Text == "public");
        Assert.Contains(sections, s => s.Color == "class" && s.Text == "Greeter");
        Assert.Contains(sections, s => s.Color == "comment" && s.Text.Contains("comment here"));
        Assert.Contains(sections, s => s.Color == "string" && s.Text.Contains("hello"));
    }

    [Fact]
    public void CSharp_preprocessor_directives_are_colored()
    {
        var sections = Highlight("csharp", "#region Demo");

        Assert.Contains(sections, s => s.Color == "preprocessor" && s.Text.Contains("#region"));
    }

    [Fact]
    public void Html_tags_attributes_strings_and_entities_are_colored()
    {
        var sections = Highlight("html", "<div class=\"x\">&amp;</div>");

        Assert.Contains(sections, s => s.Color == "attribute" && s.Text == "class");
        Assert.Contains(sections, s => s.Color == "string" && s.Text.Contains("x"));
        Assert.Contains(sections, s => s.Color == "escape" && s.Text == "&amp;");
    }

    [Fact]
    public void Xml_entities_are_colored()
    {
        var sections = Highlight("xml", "<root>&#65;</root>");

        Assert.Contains(sections, s => s.Color == "escape" && s.Text == "&#65;");
    }

    [Fact]
    public void Css_hex_colors_and_class_selectors_are_colored()
    {
        var sections = Highlight("css", "#fff { color: red; }\n.thing { color: #123456; }");

        Assert.Contains(sections, s => s.Color == "constant" && s.Text == "#fff");
        Assert.Contains(sections, s => s.Color == "constant" && s.Text == "#123456");
        Assert.Contains(sections, s => s.Color == "class" && s.Text == ".thing");
    }

    [Fact]
    public void Rust_attributes_are_colored()
    {
        var sections = Highlight("rust", "#[derive(Debug)]");

        Assert.Contains(sections, s => s.Color == "preprocessor" && s.Text.Contains("#[derive"));
    }

    [Fact]
    public void Markdown_headings_are_colored()
    {
        var sections = Highlight("markdown", "# Heading");

        Assert.Contains(sections, s => s.Color == "heading" && s.Text.Contains("Heading"));
    }

    private static void AssertCompiles(string languageId, string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.IgnorePatternWhitespace, TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            Assert.Fail($"{languageId}: invalid pattern [{pattern}]: {ex.Message}");
        }
    }

    private static bool HasUnescapedHash(string pattern)
    {
        var inCharacterClass = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var character = pattern[i];
            if (character == '\\') { i++; continue; }
            if (character == '[') { inCharacterClass = true; continue; }
            if (character == ']') { inCharacterClass = false; continue; }
            if (character == '#' && !inCharacterClass) return true;
        }
        return false;
    }

    private static List<(string Color, string Text)> Highlight(string languageId, string text)
    {
        var scheme = ColorSchemes.All.Single(s => s.Id == "vs-dark");
        var definition = new SyntaxHighlightingService().Get(LanguageCatalog.GetById(languageId), scheme);
        var document = new TextDocument(text);
        var highlighter = new DocumentHighlighter(document, definition);

        var sections = new List<(string Color, string Text)>();
        for (var lineNumber = 1; lineNumber <= document.LineCount; lineNumber++)
        {
            var line = highlighter.HighlightLine(lineNumber);
            foreach (var section in line.Sections)
                sections.Add((section.Color.Name ?? string.Empty, document.GetText(section.Offset, section.Length)));
        }

        return sections;
    }
}
