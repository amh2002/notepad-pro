using System.Text;
using NotepadPro.Core.Models;
using NotepadPro.Core.Utils;

namespace NotepadPro.Core.Services;

/// <summary>
/// Reads and writes text files while preserving encoding and line-ending choices.
/// All writes go through a temporary file so a failed save cannot truncate the original.
/// </summary>
public sealed class FileService
{
    public const long MaximumFileBytes = 200L * 1024 * 1024;

    public async Task<FileContent> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The file could not be found.", path);
        if (info.Length > MaximumFileBytes)
        {
            throw new IOException($"The file is larger than the {MaximumFileBytes / (1024 * 1024)} MB editor limit.");
        }

        byte[] bytes;
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                         FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            if (stream.Length > MaximumFileBytes)
            {
                throw new IOException($"The file is larger than the {MaximumFileBytes / (1024 * 1024)} MB editor limit.");
            }

            bytes = new byte[stream.Length];
            var total = 0;
            while (total < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(total, bytes.Length - total), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                total += read;
            }

            if (total != bytes.Length) Array.Resize(ref bytes, total);
        }

        var encoding = DetectEncoding(bytes, out var preambleLength);
        var text = Decode(bytes, encoding, preambleLength);
        var lineEnding = DetectLineEnding(text);
        var normalized = LineEndingKinds.NormalizeToLf(text);

        return new FileContent(normalized, encoding, lineEnding, info.LastWriteTimeUtc, bytes.LongLength);
    }

    public async Task<SaveResult> SaveAsync(
        string path,
        string text,
        TextEncodingKind encoding,
        LineEndingKind lineEnding,
        bool trimTrailingWhitespace = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (text is null) throw new ArgumentNullException(nameof(text));

        var info = new FileInfo(path);
        if (info.Exists && info.IsReadOnly)
        {
            throw new UnauthorizedAccessException($"'{Path.GetFileName(path)}' is read-only. Clear the read-only attribute or use Save As.");
        }

        var directory = info.DirectoryName;
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The folder '{directory}' does not exist.");
        }

        var content = trimTrailingWhitespace ? TrimTrailingWhitespace(text) : text;
        content = LineEndingKinds.Normalize(content, lineEnding);

        var encodingInstance = encoding.CreateEncoding();
        var preamble = encoding.HasPreamble() ? encodingInstance.GetPreamble() : [];
        var body = encodingInstance.GetBytes(content);

        var tempPath = Path.Combine(directory, $".{info.Name}.{Guid.NewGuid():N}.nptmp");
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             64 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                if (preamble.Length > 0) await stream.WriteAsync(preamble, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, path, overwrite: true);
            var saved = new FileInfo(path);
            return new SaveResult(saved.LastWriteTimeUtc, saved.Length);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup; the original file was never touched.
            }
            throw;
        }
    }

    public static TextEncodingKind DetectEncoding(ReadOnlySpan<byte> bytes, out int preambleLength)
    {
        preambleLength = 0;
        if (bytes.Length >= 4)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            {
                preambleLength = 4;
                return TextEncodingKind.Utf32LE;
            }
            if (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                preambleLength = 4;
                return TextEncodingKind.Utf32BE;
            }
        }
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            preambleLength = 3;
            return TextEncodingKind.Utf8Bom;
        }
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                preambleLength = 2;
                return TextEncodingKind.Utf16LE;
            }
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                preambleLength = 2;
                return TextEncodingKind.Utf16BE;
            }
        }

        var sampleLength = Math.Min(bytes.Length, 4096);
        if (sampleLength >= 16)
        {
            var evenZeros = 0;
            var oddZeros = 0;
            for (var i = 0; i < sampleLength; i++)
            {
                if (bytes[i] != 0) continue;
                if ((i & 1) == 0) evenZeros++;
                else oddZeros++;
            }

            // ASCII-heavy UTF-16 text has NULs in one byte lane. Requiring a strong
            // majority avoids misclassifying binary or mixed content.
            if (oddZeros > sampleLength / 5 && evenZeros < sampleLength / 20)
            {
                return TextEncodingKind.Utf16LE;
            }
            if (evenZeros > sampleLength / 5 && oddZeros < sampleLength / 20)
            {
                return TextEncodingKind.Utf16BE;
            }
        }

        try
        {
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return TextEncodingKind.Utf8;
        }
        catch (DecoderFallbackException)
        {
            return TextEncodingKind.Ansi;
        }
    }

    public static string Decode(ReadOnlySpan<byte> bytes, TextEncodingKind encoding, int preambleLength)
    {
        if (preambleLength > 0 && preambleLength <= bytes.Length)
        {
            bytes = bytes[preambleLength..];
        }
        return encoding.CreateEncoding().GetString(bytes);
    }

    public static LineEndingKind DetectLineEnding(string text)
    {
        if (string.IsNullOrEmpty(text)) return LineEndingKind.Windows;

        var crlf = 0;
        var lf = 0;
        var cr = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (text[i] == '\n')
            {
                lf++;
            }
        }

        if (crlf == 0 && lf == 0 && cr == 0) return LineEndingKind.Windows;
        if (crlf >= lf && crlf >= cr) return LineEndingKind.Windows;
        if (lf >= cr) return LineEndingKind.Unix;
        return LineEndingKind.Macintosh;
    }

    public static string TrimTrailingWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var builder = new StringBuilder(text.Length);
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var newlineIndex = text.IndexOf('\n', lineStart);
            var segmentEnd = newlineIndex < 0 ? text.Length : newlineIndex;
            var hasCarriageReturn = segmentEnd > lineStart && text[segmentEnd - 1] == '\r';
            var contentEnd = hasCarriageReturn ? segmentEnd - 1 : segmentEnd;
            var trimEnd = contentEnd;
            while (trimEnd > lineStart && (text[trimEnd - 1] == ' ' || text[trimEnd - 1] == '\t'))
            {
                trimEnd--;
            }

            builder.Append(text, lineStart, trimEnd - lineStart);
            if (newlineIndex >= 0)
            {
                if (hasCarriageReturn) builder.Append('\r');
                builder.Append('\n');
                lineStart = newlineIndex + 1;
            }
            else
            {
                break;
            }
        }
        return builder.ToString();
    }
}
