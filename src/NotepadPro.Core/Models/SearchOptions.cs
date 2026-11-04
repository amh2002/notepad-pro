using System.Text.RegularExpressions;

namespace NotepadPro.Core.Models;

public sealed class SearchOptions
{
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public bool UseRegex { get; set; }

    public SearchOptions Clone() => new()
    {
        MatchCase = MatchCase,
        WholeWord = WholeWord,
        UseRegex = UseRegex
    };
}

public static class SearchEngine
{
    public const int DefaultMatchLimit = 20_000;

    public static Regex? TryCreateRegex(string query, SearchOptions options, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(query)) return null;

        try
        {
            var pattern = options.UseRegex ? query : Regex.Escape(query);
            if (options.WholeWord)
            {
                pattern = $@"(?<![\p{{L}}\p{{N}}_])(?:{pattern})(?![\p{{L}}\p{{N}}_])";
            }

            var regexOptions = RegexOptions.Multiline | RegexOptions.CultureInvariant;
            if (!options.MatchCase) regexOptions |= RegexOptions.IgnoreCase;

            return new Regex(pattern, regexOptions, TimeSpan.FromSeconds(2));
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return null;
        }
    }

    public static List<(int Start, int Length)> FindAll(string text, Regex regex, int limit = DefaultMatchLimit)
    {
        var matches = new List<(int, int)>();
        if (string.IsNullOrEmpty(text)) return matches;

        try
        {
            foreach (Match match in regex.Matches(text))
            {
                if (!match.Success) break;
                matches.Add((match.Index, match.Length));
                if (matches.Count >= limit) break;
                if (match.Length == 0)
                {
                    // Zero-width matches: Matches advances automatically, this guard documents intent.
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Return what was found before the timeout; the UI reports a timed-out search.
        }

        return matches;
    }

    public static int FindNext(string text, Regex regex, int fromOffset, bool forward = true, int wrapLimit = -1)
    {
        if (string.IsNullOrEmpty(text)) return -1;
        if (forward)
        {
            var start = Math.Clamp(fromOffset, 0, text.Length);
            var match = regex.Match(text, start);
            if (!match.Success && start > 0) match = regex.Match(text, 0);
            return match.Success ? match.Index : -1;
        }

        var current = Math.Clamp(fromOffset, 0, text.Length);
        var previous = -1;
        foreach (Match match in regex.Matches(text))
        {
            if (match.Index >= current) break;
            previous = match.Index;
        }
        if (previous < 0)
        {
            foreach (Match match in regex.Matches(text)) previous = match.Index;
        }
        return previous;
    }
}
