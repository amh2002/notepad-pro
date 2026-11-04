using System.Text;
using NotepadPro.Core.Models;
using NotepadPro.Core.Services;

namespace NotepadPro.Core.Tests;

public sealed class FileServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "NotepadProTests", Guid.NewGuid().ToString("N"));
    private readonly FileService _service = new();

    public FileServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch { }
    }

    private string PathFor(string name) => Path.Combine(_directory, name);

    [Fact]
    public async Task Utf8_RoundTrips_Content_And_LineEndings()
    {
        var path = PathFor("sample.txt");
        var text = "first line\nsecond line with unicode: héllo ✓ 日本語\nthird";
        await _service.SaveAsync(path, text, TextEncodingKind.Utf8, LineEndingKind.Windows);

        var loaded = await _service.LoadAsync(path);

        Assert.Equal(TextEncodingKind.Utf8, loaded.Encoding);
        Assert.Equal(LineEndingKind.Windows, loaded.LineEnding);
        Assert.Equal("first line\nsecond line with unicode: héllo ✓ 日本語\nthird", loaded.Text);
    }

    [Fact]
    public async Task Utf8Bom_Is_Detected_And_Written()
    {
        var path = PathFor("bom.txt");
        await _service.SaveAsync(path, "hello", TextEncodingKind.Utf8Bom, LineEndingKind.Unix);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);

        var loaded = await _service.LoadAsync(path);
        Assert.Equal(TextEncodingKind.Utf8Bom, loaded.Encoding);
        Assert.Equal("hello", loaded.Text);
    }

    [Theory]
    [InlineData(TextEncodingKind.Utf16LE, LineEndingKind.Windows)]
    [InlineData(TextEncodingKind.Utf16BE, LineEndingKind.Unix)]
    [InlineData(TextEncodingKind.Utf32LE, LineEndingKind.Windows)]
    [InlineData(TextEncodingKind.Utf32BE, LineEndingKind.Macintosh)]
    public async Task Unicode_Encodings_RoundTrip(TextEncodingKind encoding, LineEndingKind lineEnding)
    {
        var path = PathFor($"unicode-{encoding}.txt");
        await _service.SaveAsync(path, "line one\nline two\n", encoding, lineEnding);

        var loaded = await _service.LoadAsync(path);
        Assert.Equal(encoding, loaded.Encoding);
        Assert.Equal(lineEnding, loaded.LineEnding);
        Assert.Equal("line one\nline two\n", loaded.Text);
    }

    [Fact]
    public async Task Ansi_Fallback_Is_Used_For_Invalid_Utf8()
    {
        var path = PathFor("ansi.txt");
        await File.WriteAllBytesAsync(path, [0x63, 0x61, 0x66, 0xE9]); // "café" in Windows-1252

        var loaded = await _service.LoadAsync(path);
        Assert.Equal(TextEncodingKind.Ansi, loaded.Encoding);
        Assert.Equal("café", loaded.Text);
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", LineEndingKind.Windows)]
    [InlineData("a\nb\nc", LineEndingKind.Unix)]
    [InlineData("a\rb\rc", LineEndingKind.Macintosh)]
    [InlineData("a\r\nb\nc", LineEndingKind.Windows)]
    public void LineEnding_Detection_Works(string text, LineEndingKind expected)
        => Assert.Equal(expected, FileService.DetectLineEnding(text));

    [Fact]
    public async Task Mixed_Line_Endings_Are_Normalized_On_Save()
    {
        var path = PathFor("mixed.txt");
        await _service.SaveAsync(path, "a\r\nb\rc\nd", TextEncodingKind.Utf8, LineEndingKind.Unix);
        var loaded = await _service.LoadAsync(path);
        Assert.Equal("a\nb\nc\nd", loaded.Text);
        Assert.Equal(LineEndingKind.Unix, loaded.LineEnding);
    }

    [Fact]
    public void Trailing_Whitespace_Trim_Preserves_Newlines()
    {
        var result = FileService.TrimTrailingWhitespace("a   \r\nb\t\n  c  ");
        Assert.Equal("a\r\nb\n  c", result);
    }

    [Fact]
    public async Task ReadOnly_File_Throws_Without_Modifying_It()
    {
        var path = PathFor("readonly.txt");
        await File.WriteAllTextAsync(path, "original");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.SaveAsync(path, "changed", TextEncodingKind.Utf8, LineEndingKind.Windows));
            Assert.Equal("original", await File.ReadAllTextAsync(path));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public async Task Save_Overwrites_Existing_File_Atomically()
    {
        var path = PathFor("overwrite.txt");
        await File.WriteAllTextAsync(path, "old");
        await _service.SaveAsync(path, "new content", TextEncodingKind.Utf8, LineEndingKind.Windows);
        Assert.Equal("new content", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.nptmp"));
    }

    [Fact]
    public async Task Empty_File_Loads_As_Utf8()
    {
        var path = PathFor("empty.txt");
        await File.WriteAllBytesAsync(path, []);
        var loaded = await _service.LoadAsync(path);
        Assert.Equal(TextEncodingKind.Utf8, loaded.Encoding);
        Assert.Equal(string.Empty, loaded.Text);
    }

    [Fact]
    public void Bomless_Utf16_Is_Heuristically_Detected()
    {
        var bytes = Encoding.Unicode.GetBytes("plain ascii text in utf16");
        var kind = FileService.DetectEncoding(bytes, out var preamble);
        Assert.Equal(TextEncodingKind.Utf16LE, kind);
        Assert.Equal(0, preamble);
    }

    [Fact]
    public async Task Missing_File_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.LoadAsync(PathFor("missing.txt")));
    }
}
