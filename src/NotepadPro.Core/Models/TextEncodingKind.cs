namespace NotepadPro.Core.Models;

public enum TextEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16LE,
    Utf16BE,
    Utf32LE,
    Utf32BE,
    Ansi
}

public static class TextEncodingKinds
{
    public static readonly TextEncodingKind[] All =
    [
        TextEncodingKind.Utf8,
        TextEncodingKind.Utf8Bom,
        TextEncodingKind.Utf16LE,
        TextEncodingKind.Utf16BE,
        TextEncodingKind.Utf32LE,
        TextEncodingKind.Utf32BE,
        TextEncodingKind.Ansi
    ];

    public static string ToDisplayName(this TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8 => "UTF-8",
        TextEncodingKind.Utf8Bom => "UTF-8 with BOM",
        TextEncodingKind.Utf16LE => "UTF-16 LE",
        TextEncodingKind.Utf16BE => "UTF-16 BE",
        TextEncodingKind.Utf32LE => "UTF-32 LE",
        TextEncodingKind.Utf32BE => "UTF-32 BE",
        TextEncodingKind.Ansi => "ANSI",
        _ => kind.ToString()
    };

    public static string ToShortName(this TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8 => "UTF-8",
        TextEncodingKind.Utf8Bom => "UTF-8 BOM",
        TextEncodingKind.Utf16LE => "UTF-16 LE",
        TextEncodingKind.Utf16BE => "UTF-16 BE",
        TextEncodingKind.Utf32LE => "UTF-32 LE",
        TextEncodingKind.Utf32BE => "UTF-32 BE",
        TextEncodingKind.Ansi => "ANSI",
        _ => kind.ToString()
    };

    public static bool HasPreamble(this TextEncodingKind kind) => kind is not (TextEncodingKind.Utf8 or TextEncodingKind.Ansi);

    public static System.Text.Encoding CreateEncoding(this TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Utf8 => new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
        TextEncodingKind.Utf8Bom => new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false),
        TextEncodingKind.Utf16LE => new System.Text.UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: false),
        TextEncodingKind.Utf16BE => new System.Text.UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: false),
        TextEncodingKind.Utf32LE => new System.Text.UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: false),
        TextEncodingKind.Utf32BE => new System.Text.UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: false),
        TextEncodingKind.Ansi => TextEncodings.Ansi,
        _ => new System.Text.UTF8Encoding(false)
    };
}

public enum LineEndingKind
{
    Windows,
    Unix,
    Macintosh
}

public static class LineEndingKinds
{
    public static readonly LineEndingKind[] All =
    [
        LineEndingKind.Windows,
        LineEndingKind.Unix,
        LineEndingKind.Macintosh
    ];

    public static string ToDisplayName(this LineEndingKind kind) => kind switch
    {
        LineEndingKind.Windows => "CRLF (Windows)",
        LineEndingKind.Unix => "LF (Unix / macOS)",
        LineEndingKind.Macintosh => "CR (Classic Mac)",
        _ => kind.ToString()
    };

    public static string ToShortName(this LineEndingKind kind) => kind switch
    {
        LineEndingKind.Windows => "CRLF",
        LineEndingKind.Unix => "LF",
        LineEndingKind.Macintosh => "CR",
        _ => kind.ToString()
    };

    public static string ToCharacters(this LineEndingKind kind) => kind switch
    {
        LineEndingKind.Windows => "\r\n",
        LineEndingKind.Unix => "\n",
        LineEndingKind.Macintosh => "\r",
        _ => "\n"
    };

    /// <summary>Converts every recognised line break into the requested style in a single pass.</summary>
    public static string Normalize(string text, LineEndingKind kind)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var replacement = kind.ToCharacters();
        var result = new System.Text.StringBuilder(text.Length + 32);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                result.Append(replacement);
            }
            else if (ch == '\n')
            {
                result.Append(replacement);
            }
            else
            {
                result.Append(ch);
            }
        }
        return result.ToString();
    }

    /// <summary>Normalises all breaks to LF, the format AvalonEdit edits most predictably.</summary>
    public static string NormalizeToLf(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                result.Append('\n');
            }
            else
            {
                result.Append(ch);
            }
        }
        return result.ToString();
    }
}
