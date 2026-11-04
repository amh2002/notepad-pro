using System.Text;

namespace NotepadPro.Core.Models;

internal static class TextEncodings
{
    static TextEncodings()
    {
        // Makes legacy single/double-byte code pages available on .NET.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly Encoding AnsiEncodingInstance = CreateAnsi();

    public static Encoding Ansi => AnsiEncodingInstance;

    private static Encoding CreateAnsi()
    {
        try
        {
            return Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        }
        catch (Exception)
        {
            return Encoding.Latin1;
        }
    }
}
