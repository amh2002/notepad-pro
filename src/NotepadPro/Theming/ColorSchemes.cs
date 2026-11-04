namespace NotepadPro.Theming;

/// <summary>Built-in editor colour schemes, inspired by popular developer editors.</summary>
public static class ColorSchemes
{
    public const string AutoId = "auto";

    public static IReadOnlyList<EditorColorScheme> All { get; } =
    [
        new EditorColorScheme
        {
            Id = "vs-light",
            DisplayName = "Visual Studio Light",
            Description = "Clean, high-contrast light palette inspired by Visual Studio.",
            IsDark = false,
            Background = "#FFFFFF",
            Foreground = "#1E1E1E",
            Gutter = "#F7F7F7",
            LineNumber = "#8C8C8C",
            CurrentLine = "#F3F7FB",
            CurrentLineBorder = "#E3EDF7",
            Selection = "#ADD6FF",
            SelectionBorder = "#99C7F5",
            IndentGuide = "#E0E0E0",
            Bracket = "#8A8A8A",
            BracketBackground = "#DDEBF7",
            SearchMatch = "#FFF1A8",
            SearchCurrent = "#FFC94D",
            Caret = "#0067C0",
            PanelBackground = "#FFFFFF",
            PanelBorder = "#D7D7D7",
            Syntax = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["comment"] = "#008000",
                ["keyword"] = "#0000FF",
                ["string"] = "#A31515",
                ["number"] = "#098658",
                ["type"] = "#267F99",
                ["class"] = "#267F99",
                ["function"] = "#795E26",
                ["variable"] = "#001080",
                ["operator"] = "#1E1E1E",
                ["constant"] = "#0070C1",
                ["builtin"] = "#795E26",
                ["property"] = "#0451A5",
                ["tag"] = "#800000",
                ["attribute"] = "#E50000",
                ["preprocessor"] = "#808080",
                ["regex"] = "#811F3F",
                ["escape"] = "#D16969",
                ["todo"] = "#D97706",
                ["heading"] = "#800000",
                ["link"] = "#0563C1",
                ["code"] = "#A31515",
                ["emphasis"] = "#800000",
                ["quote"] = "#008000"
            }
        },
        new EditorColorScheme
        {
            Id = "vs-dark",
            DisplayName = "Visual Studio Dark",
            Description = "Balanced, familiar dark palette with gentle contrast for long sessions.",
            IsDark = true,
            Background = "#1E1E1E",
            Foreground = "#D4D4D4",
            Gutter = "#1E1E1E",
            LineNumber = "#858585",
            CurrentLine = "#282828",
            CurrentLineBorder = "#333333",
            Selection = "#264F78",
            SelectionBorder = "#3A6EA5",
            IndentGuide = "#404040",
            Bracket = "#A0A0A0",
            BracketBackground = "#3A3A3A",
            SearchMatch = "#613214",
            SearchCurrent = "#9E6A03",
            Caret = "#AEAFAD",
            PanelBackground = "#252526",
            PanelBorder = "#3F3F46",
            Syntax = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["comment"] = "#6A9955",
                ["keyword"] = "#569CD6",
                ["string"] = "#CE9178",
                ["number"] = "#B5CEA8",
                ["type"] = "#4EC9B0",
                ["class"] = "#4EC9B0",
                ["function"] = "#DCDCAA",
                ["variable"] = "#9CDCFE",
                ["operator"] = "#D4D4D4",
                ["constant"] = "#569CD6",
                ["builtin"] = "#4FC1FF",
                ["property"] = "#9CDCFE",
                ["tag"] = "#569CD6",
                ["attribute"] = "#9CDCFE",
                ["preprocessor"] = "#9B9B9B",
                ["regex"] = "#D16969",
                ["escape"] = "#D7BA7D",
                ["todo"] = "#FFC66D",
                ["heading"] = "#569CD6",
                ["link"] = "#4FC1FF",
                ["code"] = "#CE9178",
                ["emphasis"] = "#D7BA7D",
                ["quote"] = "#6A9955"
            }
        },
        new EditorColorScheme
        {
            Id = "one-dark",
            DisplayName = "One Dark",
            Description = "A soft, rounded dark theme inspired by Atom One Dark.",
            IsDark = true,
            Background = "#282C34",
            Foreground = "#ABB2BF",
            Gutter = "#282C34",
            LineNumber = "#636D83",
            CurrentLine = "#2C313C",
            CurrentLineBorder = "#333844",
            Selection = "#3E4451",
            SelectionBorder = "#4B5263",
            IndentGuide = "#3B4048",
            Bracket = "#ABB2BF",
            BracketBackground = "#3E4451",
            SearchMatch = "#5C4B2E",
            SearchCurrent = "#82693D",
            Caret = "#528BFF",
            PanelBackground = "#21252B",
            PanelBorder = "#3A3F4B",
            Syntax = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["comment"] = "#7F848E",
                ["keyword"] = "#C678DD",
                ["string"] = "#98C379",
                ["number"] = "#D19A66",
                ["type"] = "#E5C07B",
                ["class"] = "#E5C07B",
                ["function"] = "#61AFEF",
                ["variable"] = "#E06C75",
                ["operator"] = "#56B6C2",
                ["constant"] = "#D19A66",
                ["builtin"] = "#56B6C2",
                ["property"] = "#E06C75",
                ["tag"] = "#E06C75",
                ["attribute"] = "#D19A66",
                ["preprocessor"] = "#C678DD",
                ["regex"] = "#C678DD",
                ["escape"] = "#56B6C2",
                ["todo"] = "#E5C07B",
                ["heading"] = "#E06C75",
                ["link"] = "#61AFEF",
                ["code"] = "#98C379",
                ["emphasis"] = "#E5C07B",
                ["quote"] = "#7F848E"
            }
        },
        new EditorColorScheme
        {
            Id = "github-light",
            DisplayName = "GitHub Light",
            Description = "Crisp light palette inspired by GitHub's web editor.",
            IsDark = false,
            Background = "#FFFFFF",
            Foreground = "#24292F",
            Gutter = "#FFFFFF",
            LineNumber = "#8C959F",
            CurrentLine = "#F6F8FA",
            CurrentLineBorder = "#EAEEF2",
            Selection = "#B6D7FF",
            SelectionBorder = "#8BB9F0",
            IndentGuide = "#EAEEF2",
            Bracket = "#57606A",
            BracketBackground = "#DDEBF7",
            SearchMatch = "#FFF8C5",
            SearchCurrent = "#F9D77E",
            Caret = "#0969DA",
            PanelBackground = "#FFFFFF",
            PanelBorder = "#D0D7DE",
            Syntax = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["comment"] = "#6E7781",
                ["keyword"] = "#CF222E",
                ["string"] = "#0A3069",
                ["number"] = "#0550AE",
                ["type"] = "#953800",
                ["class"] = "#953800",
                ["function"] = "#8250DF",
                ["variable"] = "#24292F",
                ["operator"] = "#CF222E",
                ["constant"] = "#0550AE",
                ["builtin"] = "#0550AE",
                ["property"] = "#0550AE",
                ["tag"] = "#116329",
                ["attribute"] = "#0550AE",
                ["preprocessor"] = "#6E7781",
                ["regex"] = "#116329",
                ["escape"] = "#0550AE",
                ["todo"] = "#953800",
                ["heading"] = "#0550AE",
                ["link"] = "#0969DA",
                ["code"] = "#0A3069",
                ["emphasis"] = "#24292F",
                ["quote"] = "#6E7781"
            }
        },
        new EditorColorScheme
        {
            Id = "github-dark",
            DisplayName = "GitHub Dark",
            Description = "Deep neutral dark palette with vivid but restrained accents.",
            IsDark = true,
            Background = "#0D1117",
            Foreground = "#C9D1D9",
            Gutter = "#0D1117",
            LineNumber = "#6E7681",
            CurrentLine = "#161B22",
            CurrentLineBorder = "#21262D",
            Selection = "#264F78",
            SelectionBorder = "#3A6EA5",
            IndentGuide = "#30363D",
            Bracket = "#C9D1D9",
            BracketBackground = "#30363D",
            SearchMatch = "#4B3B17",
            SearchCurrent = "#8A6A1F",
            Caret = "#58A6FF",
            PanelBackground = "#161B22",
            PanelBorder = "#30363D",
            Syntax = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["comment"] = "#8B949E",
                ["keyword"] = "#FF7B72",
                ["string"] = "#A5D6FF",
                ["number"] = "#79C0FF",
                ["type"] = "#FFA657",
                ["class"] = "#FFA657",
                ["function"] = "#D2A8FF",
                ["variable"] = "#C9D1D9",
                ["operator"] = "#FF7B72",
                ["constant"] = "#79C0FF",
                ["builtin"] = "#79C0FF",
                ["property"] = "#79C0FF",
                ["tag"] = "#7EE787",
                ["attribute"] = "#79C0FF",
                ["preprocessor"] = "#8B949E",
                ["regex"] = "#7EE787",
                ["escape"] = "#79C0FF",
                ["todo"] = "#D29922",
                ["heading"] = "#79C0FF",
                ["link"] = "#58A6FF",
                ["code"] = "#A5D6FF",
                ["emphasis"] = "#C9D1D9",
                ["quote"] = "#8B949E"
            }
        }
    ];

    public static EditorColorScheme GetById(string? id) =>
        All.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) ?? All[1];

    public static EditorColorScheme Resolve(string? id, bool dark)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Equals(AutoId, StringComparison.OrdinalIgnoreCase))
            return GetById(dark ? "vs-dark" : "vs-light");
        return GetById(id);
    }

    public static string ResolveId(string? id, bool dark) => Resolve(id, dark).Id;
}
