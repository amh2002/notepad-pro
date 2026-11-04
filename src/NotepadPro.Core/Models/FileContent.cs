namespace NotepadPro.Core.Models;

public sealed record FileContent(
    string Text,
    TextEncodingKind Encoding,
    LineEndingKind LineEnding,
    DateTime LastWriteTimeUtc,
    long ByteLength)
{
    public string EncodingDisplayName => Encoding.ToDisplayName();
    public string LineEndingDisplayName => LineEnding.ToShortName();
}

public sealed record SaveResult(DateTime LastWriteTimeUtc, long ByteLength);
