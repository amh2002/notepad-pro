using NotepadPro.Core.Models;
using NotepadPro.Core.Services;

namespace NotepadPro.Core.Tests;

public sealed class LanguageAndSearchTests
{
    [Theory]
    [InlineData("file.cs", "csharp")]
    [InlineData("FILE.CS", "csharp")]
    [InlineData("main.cpp", "cpp")]
    [InlineData("app.tsx", "typescript")]
    [InlineData("index.html", "html")]
    [InlineData("style.scss", "scss")]
    [InlineData("config.yaml", "yaml")]
    [InlineData("script.ps1", "powershell")]
    [InlineData("notes.txt", "plaintext")]
    [InlineData("unknown.zzz", "plaintext")]
    [InlineData(null, "plaintext")]
    public void Extension_Detection_Works(string? path, string expected)
        => Assert.Equal(expected, LanguageCatalog.DetectFromPath(path).Id);

    [Theory]
    [InlineData("csharp")]
    [InlineData("c")]
    [InlineData("cpp")]
    [InlineData("java")]
    [InlineData("javascript")]
    [InlineData("typescript")]
    [InlineData("python")]
    [InlineData("html")]
    [InlineData("css")]
    [InlineData("scss")]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("sql")]
    [InlineData("php")]
    [InlineData("go")]
    [InlineData("rust")]
    [InlineData("bash")]
    [InlineData("powershell")]
    [InlineData("yaml")]
    [InlineData("markdown")]
    [InlineData("plaintext")]
    public void Required_Languages_Exist(string id) => Assert.Equal(id, LanguageCatalog.GetById(id).Id);

    [Fact]
    public void Unknown_Language_Falls_Back_To_Plain_Text()
        => Assert.Equal(LanguageCatalog.PlainTextId, LanguageCatalog.GetById("not-a-language").Id);

    [Fact]
    public void Search_Is_Case_Insensitive_By_Default()
    {
        var regex = SearchEngine.TryCreateRegex("hello", new SearchOptions(), out _);
        Assert.NotNull(regex);
        var matches = SearchEngine.FindAll("Hello hello HELLO", regex);
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void Search_Respects_Case_And_Whole_Word()
    {
        var options = new SearchOptions { MatchCase = true, WholeWord = true };
        var regex = SearchEngine.TryCreateRegex("cat", options, out _)!;
        var matches = SearchEngine.FindAll("cat Cat concatenate cat.", regex);
        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.Equal("cat", "cat Cat concatenate cat.".Substring(m.Start, m.Length)));
    }

    [Fact]
    public void Regex_Search_Works()
    {
        var options = new SearchOptions { UseRegex = true };
        var regex = SearchEngine.TryCreateRegex(@"\b\d{3}-\d{2}-\d{4}\b", options, out _)!;
        var matches = SearchEngine.FindAll("SSN 123-45-6789, other 12-34-5678", regex);
        Assert.Single(matches);
        Assert.Equal(4, matches[0].Start);
    }

    [Fact]
    public void Invalid_Regex_Returns_Error()
    {
        var regex = SearchEngine.TryCreateRegex("(", new SearchOptions { UseRegex = true }, out var error);
        Assert.Null(regex);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void FindNext_Wraps_And_FindPrevious_Wraps()
    {
        var regex = SearchEngine.TryCreateRegex("x", new SearchOptions(), out _)!;
        Assert.Equal(6, SearchEngine.FindNext("a x a x", regex, 5));
        Assert.Equal(2, SearchEngine.FindNext("a x a x", regex, 5, forward: false));
        Assert.Equal(2, SearchEngine.FindNext("a x a x", regex, 0));
        Assert.Equal(6, SearchEngine.FindNext("a x a x", regex, 99, forward: false));
    }

    [Fact]
    public void Zero_Length_Regex_Does_Not_Hang()
    {
        var regex = SearchEngine.TryCreateRegex("^", new SearchOptions { UseRegex = true }, out _)!;
        var matches = SearchEngine.FindAll("a\nb\nc", regex);
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void LineEnding_Normalization_Handles_All_Styles()
    {
        Assert.Equal("a\r\nb\r\nc\r\n", LineEndingKinds.Normalize("a\rb\nc\r\n", LineEndingKind.Windows));
        Assert.Equal("a\nb\nc\n", LineEndingKinds.Normalize("a\rb\nc\r\n", LineEndingKind.Unix));
        Assert.Equal("a\rb\rc\r", LineEndingKinds.Normalize("a\rb\nc\r\n", LineEndingKind.Macintosh));
    }
}
