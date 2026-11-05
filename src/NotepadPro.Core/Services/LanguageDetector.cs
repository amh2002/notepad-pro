using System.Text.Json;
using System.Text.RegularExpressions;
using NotepadPro.Core.Models;

namespace NotepadPro.Core.Services;

/// <summary>
/// Guesses a language from document content using weighted, language-specific signals.
/// Detection is deliberately conservative: when the sample is short or two languages are
/// equally plausible, <see cref="Detect"/> returns <c>null</c> instead of guessing.
/// </summary>
public static class LanguageDetector
{
    /// <summary>Only the beginning of a document is inspected; enough to identify a language.</summary>
    public const int MaxSampleLength = 20_000;

    private const double MinimumScore = 3.0;
    private const double MinimumMargin = 1.0;

    private sealed record Signal(string LanguageId, Regex Pattern, double Weight, int MaxMatches = 1);

    private static Regex R(string pattern, RegexOptions extra = RegexOptions.None) =>
        new(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant | extra,
            TimeSpan.FromMilliseconds(500));

    private static readonly RegexOptions IgnoreCase = RegexOptions.IgnoreCase;

    private static readonly Signal[] Signals =
    [
        // ---------------------------------------------------------------- C#
        new("csharp", R(@"\busing\s+System(?:\.[\w.]+)?\s*;"), 3.0),
        new("csharp", R(@"\bConsole\.(?:Write|WriteLine|ReadLine|ReadKey)\s*\("), 3.0),
        new("csharp", R(@"\b(?:public|private|protected|internal)\s+(?:static\s+)?(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+\w+"), 1.5),
        new("csharp", R(@"\bnamespace\s+[\w.]+(?:;|\s*\{)"), 1.5),
        new("csharp", R(@"\{\s*get\s*;\s*(?:set\s*;)?\s*\}|\b(?:get|set)\s*;\s*(?:get|set)\s*;"), 2.5),
        new("csharp", R(@"\basync\s+Task(?:<[^>]+>)?\s+\w+\s*\("), 3.0),
        new("csharp", R(@"\bvar\s+\w+\s*="), 1.5),
        new("csharp", R(@"\brecord\s+\w+(?:\s*\(|\s*\{)"), 2.0),
        new("csharp", R(@"\bnameof\s*\("), 2.0),
        new("csharp", R(@"#(?:region|endregion)\b"), 2.0),
        new("csharp", R(@"\b(?:public|private|protected|internal)\s+[\w<>,\[\]?]+\s+\w+\s*\{" ), 1.0),
        new("csharp", R(@"\b(?:string|int|bool|double|decimal)\?\s+\w+"), 1.0),
        new("csharp", R(@"\[(?:Serializable|Obsolete|JsonProperty|Required|Fact|Test|HttpGet|HttpPost|FromBody|FromServices|Range|MaxLength)\b"), 2.0),
        new("csharp", R(@"\b(?:IEnumerable|IReadOnlyList|IList|IDictionary|IServiceCollection|ILogger)\b"), 1.5),
        new("csharp", R(@"\busing\s+[A-Z][\w.]*\s*;"), 1.5),
        new("csharp", R(@"\b(?:base|this)\.\w+\s*\("), 1.0),

        // ---------------------------------------------------------------- Java
        new("java", R(@"\bimport\s+java(?:x)?\."), 4.0),
        new("java", R(@"\bSystem\.(?:out|err)\.print"), 4.0),
        new("java", R(@"\bpublic\s+static\s+void\s+main\s*\(\s*String(?:\[\]|\.\.\.)\s*\w+"), 4.0),
        new("java", R(@"^\s*@(?:Override|Deprecated|SuppressWarnings|FunctionalInterface|SafeVarargs)\b"), 3.0),
        new("java", R(@"\bpackage\s+[\w.]+\s*;"), 3.0),
        new("java", R(@"\b(?:ArrayList|HashMap|HashSet|LinkedList|TreeMap|Optional|Stream)<"), 2.0),
        new("java", R(@"\b(?:extends|implements)\s+[A-Z]\w*"), 1.5),
        new("java", R(@"\bthrows\s+\w+(?:Exception|Error)\b"), 2.0),
        new("java", R(@"\bprivate\s+final\s+\w+"), 1.5),
        new("java", R(@"\bString\[\]\s+\w+"), 1.5),
        new("java", R(@"\b(?:Boolean|Integer|Double|Long|Character)\.\w+\("), 1.5),
        new("java", R(@"\bpublic\s+(?:final\s+)?class\s+\w+"), 1.0),

        // ---------------------------------------------------------------- C
        new("c", R(@"#include\s*<(?:stdio|stdlib|string|math|ctype|time|assert|errno|stdint|stdbool|stddef|limits|float|signal|locale|setjmp|stdarg)\.h>"), 4.0),
        new("c", R(@"\bint\s+main\s*\(\s*(?:void|int\s+argc)"), 2.5),
        new("c", R(@"\bprintf\s*\("), 1.5),
        new("c", R(@"\b(?:scanf|sscanf|fscanf)\s*\("), 2.0),
        new("c", R(@"\b(?:malloc|calloc|realloc|free)\s*\("), 1.0),
        new("c", R(@"\btypedef\s+(?:struct|enum|union)\b"), 2.0),
        new("c", R(@"#define\s+[A-Z_]\w*"), 1.0),
        new("c", R(@"\b(?:size_t|uint8_t|uint16_t|uint32_t|uint64_t|int32_t|int64_t)\b"), 1.0),
        new("c", R(@"\bstruct\s+\w+\s*\{"), 1.0),
        new("c", R(@"\bNULL\b"), 0.5),
        new("c", R(@"\b(?:std::|cout\s*<<|cin\s*>>|using\s+namespace|template\s*<|#include\s*<(?:iostream|vector|string|map|memory|algorithm)>)"), -3.0),

        // ---------------------------------------------------------------- C++
        new("cpp", R(@"#include\s*<(?:iostream|vector|string|map|set|memory|algorithm|utility|fstream|sstream|unordered_map|unordered_set|array|deque|list|queue|stack|chrono|thread|mutex|atomic|optional|variant|tuple|functional|numeric|iomanip)>"), 4.0),
        new("cpp", R(@"\busing\s+namespace\s+std\s*;"), 4.0),
        new("cpp", R(@"\bstd::"), 3.0),
        new("cpp", R(@"\b(?:cout|cin|cerr)\s*(?:<<|>>)"), 3.0),
        new("cpp", R(@"\btemplate\s*<"), 3.0),
        new("cpp", R(@"^\s*(?:public|private|protected)\s*:"), 3.0),
        new("cpp", R(@"\bnullptr\b"), 2.0),
        new("cpp", R(@"\b(?:unique_ptr|shared_ptr|weak_ptr|make_unique|make_shared)\b"), 2.0),
        new("cpp", R(@"\b(?:std::)?(?:vector|map|set|string|unordered_map|unordered_set)<"), 2.0),
        new("cpp", R(@"\bvirtual\s+[\w:<>]+\s+\w+\s*\("), 1.5),
        new("cpp", R(@"\bclass\s+\w+\s*:\s*(?:public|private|protected)\b"), 2.0),
        new("cpp", R(@"#include\s*<c(?:stdio|stdlib|string|math|stdint)>"), 2.0),

        // ---------------------------------------------------------------- Python
        new("python", R(@"^\s*def\s+\w+\s*\("), 4.0),
        new("python", R(@"^\s*(?:from\s+[\w.]+\s+import\s+|import\s+[\w.]+(?:\s+as\s+\w+)?\s*$)"), 2.5),
        new("python", R(@"\bif\s+__name__\s*==\s*['""]__main__['""]"), 5.0),
        new("python", R(@"^\s*(?:elif|except|finally)\b"), 2.0),
        new("python", R(@"\bself\.\w+"), 2.5),
        new("python", R(@"^\s*class\s+\w+\s*(?:\([^)]*\))?\s*:"), 3.0),
        new("python", R(@"^\s*@\w+(?:\.\w+)*\s*$"), 1.5),
        new("python", R(@"\bNone\b"), 0.75),
        new("python", R(@"\b(?:True|False)\b"), 0.75),
        new("python", R(@"\bprint\s*\("), 0.75),
        new("python", R(@"\bf['""]"), 0.75),
        new("python", R(@"^\s*#\s*.*$"), 0.25),
        new("python", R(@"\b(?:lambda|yield)\b|\bwith\s+open\s*\("), 1.0),

        // ---------------------------------------------------------------- JavaScript
        new("javascript", R(@"\b(?:const|let)\s+\w+\s*="), 1.5),
        new("javascript", R(@"\brequire\s*\("), 2.5),
        new("javascript", R(@"\bmodule\.exports\b|\bexports\.\w+\s*="), 3.0),
        new("javascript", R(@"\bconsole\.(?:log|error|warn|info|debug|table)\s*\("), 3.0),
        new("javascript", R(@"\b(?:document|window)\.\w+"), 2.0),
        new("javascript", R(@"\b(?:querySelector|getElementById|addEventListener)\s*\("), 2.0),
        new("javascript", R(@"\bfunction\s*\w*\s*\("), 1.5),
        new("javascript", R(@"=>"), 1.0),
        new("javascript", R(@"\b(?:async\s+function|await\s+)"), 1.5),
        new("javascript", R(@"\bexport\s+(?:default|const|function|class|let|var)\b"), 2.0),
        new("javascript", R(@"\bnew\s+Promise\s*\("), 2.0),
        new("javascript", R(@"\bJSON\.(?:parse|stringify)\s*\("), 2.0),
        new("javascript", R(@"\b(?:fetch|axios)\s*\("), 1.0),
        new("javascript", R(@"\b(?:process|module)\.\w+"), 1.0),
        new("javascript", R(@"===|!=="), 0.75),
        new("javascript", R(@"\bapp\.(?:get|post|put|patch|delete|use|listen)\s*\("), 1.5),

        // ---------------------------------------------------------------- TypeScript
        new("typescript", R(@"\b(?:const|let|var)\s+\w+\s*:\s*(?:string|number|boolean|any|unknown|never|void|[A-Z]\w*(?:<[^>]+>)?)\b"), 3.0),
        new("typescript", R(@"\(\s*\w+\s*:\s*(?:string|number|boolean|any|unknown|[A-Z]\w*)"), 2.0),
        new("typescript", R(@"\)\s*:\s*(?:Promise<[^>]+>|void|[A-Z]\w*(?:<[^>]+>)?|[a-z]+\[\])\s*[={]"), 2.0),
        new("typescript", R(@"\binterface\s+\w+"), 2.5),
        new("typescript", R(@"\btype\s+\w+\s*="), 2.0),
        new("typescript", R(@"\benum\s+\w+\s*\{"), 1.5),
        new("typescript", R(@"\b(?:readonly|public|private|protected)\s+\w+\s*[:?]"), 1.5),
        new("typescript", R(@"\bas\s+(?:const|unknown|string|number|boolean|[A-Z]\w*)"), 1.5),
        new("typescript", R(@"\bimplements\s+\w+"), 1.5),
        new("typescript", R(@"\b(?:string|number|boolean|any|unknown|never)\[\]"), 1.5),
        new("typescript", R(@"\b(?:Partial|Readonly|Record|Pick|Omit|Required|Promise|Array)<"), 1.5),
        new("typescript", R(@"\bdeclare\s+(?:const|function|module|global)\b"), 2.0),
        new("typescript", R(@"\bexport\s+(?:interface|type|enum)\b"), 2.0),
        new("typescript", R(@"\b(?:keyof|infer|satisfies)\b"), 1.5),

        // ---------------------------------------------------------------- HTML
        new("html", R(@"<!doctype\s+html", IgnoreCase), 6.0),
        new("html", R(@"<html(?:\s|>)", IgnoreCase), 5.0),
        new("html", R(@"</html\s*>", IgnoreCase), 4.0),
        new("html", R(@"<(?:head|body)(?:\s|>)", IgnoreCase), 3.0),
        new("html", R(@"<(?:div|span|p|a|ul|ol|li|table|section|article|header|footer|nav|form|input|button|img|h[1-6])(?:\s|/?>)", IgnoreCase), 2.0),
        new("html", R(@"</(?:div|span|p|a|ul|ol|li|body|head|html|section|article|header|footer|nav|form|table)\s*>", IgnoreCase), 1.5),
        new("html", R(@"<(?:meta|link|script|style|title)(?:\s|>)", IgnoreCase), 1.0),
        new("html", R(@"\b(?:class|id|href|src|alt|type|rel|data-[\w-]+)\s*=", IgnoreCase), 0.75),

        // ---------------------------------------------------------------- XML
        new("xml", R(@"^\s*<\?xml\s+"), 6.0),
        new("xml", R(@"\bxmlns(?::[\w.-]+)?\s*="), 3.0),
        new("xml", R(@"</[A-Za-z_][\w.:-]*\s*>"), 1.5, 3),
        new("xml", R(@"<[A-Za-z_][\w.:-]*(?:\s+[\w.:-]+\s*=\s*[""'][^""']*[""'])*\s*/>"), 1.5),
        new("xml", R(@"<!--[\s\S]*?-->"), 0.5),
        new("xml", R(@"<!\[CDATA\["), 2.0),
        new("xml", R(@"<!DOCTYPE\s+(?!html)", IgnoreCase), 2.0),
        new("xml", R(@"<(?:configuration|appSettings|project|PropertyGroup|ItemGroup|root|note|book|person|employee|svg)(?:\s|>)", IgnoreCase), 2.0),
        new("xml", R(@"<[A-Za-z_][\w.-]*:[A-Za-z_][\w.-]*"), 1.0),

        // ---------------------------------------------------------------- CSS
        new("css", R(@"(?:^|\n)\s*[.#][\w-]+(?:[\s.:>+~,\w-]*)\s*\{"), 2.0),
        new("css", R(@"(?:^|\n)\s*(?:body|html|div|span|p|a|ul|ol|li|table|header|footer|nav|section|article|h[1-6]|input|button|form|img)\s*(?:[,>+~][^{]*)?\{"), 1.5),
        new("css", R(@"(?:^|\n)\s*(?:color|background(?:-color|-image)?|margin(?:-\w+)?|padding(?:-\w+)?|font(?:-\w+)?|display|border(?:-\w+)?|width|height|max-width|min-width|position|top|right|bottom|left|z-index|opacity|transform|transition|box-shadow|flex|grid(?:-\w+)?|gap|align-items|justify-content|text-align|line-height|letter-spacing|overflow|visibility|cursor|content)\s*:"), 2.5),
        new("css", R(@"@(?:media|import|keyframes|font-face|supports|charset)\b"), 2.0),
        new("css", R(@"^\s*:root\s*\{"), 2.0),
        new("css", R(@"\b(?:hover|focus|active|visited|first-child|last-child|nth-child)\b\s*(?:\{|,)"), 1.0),
        new("css", R(@"\b(?:rgba?|hsla?|calc|var)\s*\("), 0.75),
        new("css", R(@"!important\b"), 1.0),

        // ---------------------------------------------------------------- SCSS
        new("scss", R(@"\$[\w-]+\s*:"), 4.0),
        new("scss", R(@"@(?:mixin|include|extend|use|forward|function|each|for|while|if|else)\b"), 3.0),
        new("scss", R(@"&(?::|\.|\[|\s*\{)"), 2.0),
        new("scss", R(@"%[\w-]+\s*\{"), 2.0),
        new("scss", R(@"\b(?:darken|lighten|mix|map-get|map-merge)\s*\("), 2.0),
        new("scss", R(@"\b@include\b[\s\S]{0,200}?\$"), 1.0),

        // ---------------------------------------------------------------- JSON
        new("json", R(@"^\s*[\[{]\s*""[^""\\]*(?:\\.[^""\\]*)*""\s*:"), 3.0),
        new("json", R(@"""[^""\\]*(?:\\.[^""\\]*)*""\s*:"), 1.5, 6),
        new("json", R(@"[:,]\s*(?:true|false|null)\b"), 0.5),
        new("json", R(@"^\s*[\[{][\s\S]*[\]}]\s*$"), 1.0),

        // ---------------------------------------------------------------- SQL
        new("sql", R(@"\bselect\b[\s\S]{0,400}?\bfrom\b", IgnoreCase), 5.0),
        new("sql", R(@"\binsert\s+into\b", IgnoreCase), 3.0),
        new("sql", R(@"\bupdate\s+\w+\s+set\b", IgnoreCase), 3.0),
        new("sql", R(@"\bdelete\s+from\b", IgnoreCase), 3.0),
        new("sql", R(@"\bcreate\s+(?:table|database|index|view|procedure|function|schema)\b", IgnoreCase), 3.0),
        new("sql", R(@"\b(?:left|right|inner|outer|full|cross)?\s*join\b", IgnoreCase), 1.0),
        new("sql", R(@"\bwhere\b", IgnoreCase), 1.0),
        new("sql", R(@"\b(?:group|order)\s+by\b", IgnoreCase), 1.5),
        new("sql", R(@"\bvalues\s*\(", IgnoreCase), 1.0),
        new("sql", R(@"\b(?:primary|foreign)\s+key\b", IgnoreCase), 1.0),

        // ---------------------------------------------------------------- PHP
        new("php", R(@"<\?php\b|<\?="), 8.0),
        new("php", R(@"\$\w+\s*(?:=|\.=|\+=)"), 1.0),
        new("php", R(@"\b(?:echo|print)\s+"), 1.0),
        new("php", R(@"\b(?:strlen|str_replace|substr|explode|implode|count|array_map|array_filter|array_merge|in_array|json_encode|json_decode|preg_match|preg_replace|file_get_contents|htmlspecialchars|var_dump|print_r)\s*\("), 2.0),
        new("php", R(@"\bfunction\s+\w+\s*\([^)]*\)\s*(?::\s*\w+\s*)?\{"), 0.75),
        new("php", R(@"->\w+\s*\("), 0.5),
        new("php", R(@"\$this->"), 2.0),

        // ---------------------------------------------------------------- Go
        new("go", R(@"^\s*package\s+\w+\s*$"), 5.0),
        new("go", R(@"^\s*func\s+(?:\([^)]*\)\s*)?\w+\s*\("), 4.0),
        new("go", R(@"\bfmt\.(?:Print|Printf|Println|Sprint|Sprintf|Errorf|Fprint)"), 3.0),
        new("go", R(@":="), 2.0),
        new("go", R(@"^\s*import\s*\("), 3.0),
        new("go", R(@"\bdefer\s+"), 2.0),
        new("go", R(@"\bgo\s+(?:func\b|\w+\()"), 2.0),
        new("go", R(@"\btype\s+\w+\s+(?:struct|interface)\s*\{"), 2.5),
        new("go", R(@"\bchan\s+[\w*\[\]]+"), 2.0),
        new("go", R(@"\brange\s+\w+"), 1.0),
        new("go", R(@"\berror\s*\)|\berror\s*\{"), 0.5),

        // ---------------------------------------------------------------- Rust
        new("rust", R(@"^\s*fn\s+\w+\s*\("), 4.0),
        new("rust", R(@"\blet\s+mut\s+\w+"), 4.0),
        new("rust", R(@"\b(?:println|print|eprintln)!\s*\("), 4.0),
        new("rust", R(@"\buse\s+std::"), 4.0),
        new("rust", R(@"\bimpl(?:\s*<[^>]*>)?\s+\w+"), 3.0),
        new("rust", R(@"\bpub\s+(?:fn|struct|enum|trait|mod|const|static)\b"), 2.0),
        new("rust", R(@"#\[(?:derive|cfg|test|allow|warn|repr)\b"), 4.0),
        new("rust", R(@"\b(?:Some|None|Ok|Err)\s*\("), 1.0),
        new("rust", R(@"->\s*(?:Self|Result|Option|[A-Z]\w*)\b"), 1.0),
        new("rust", R(@"&(?:mut\s+)?str\b"), 1.0),
        new("rust", R(@"\bmatch\s+\w+\s*\{"), 1.0),
        new("rust", R(@"\bmod\s+\w+\s*;"), 1.0),
        new("rust", R(@"\b(?:i8|i16|i32|i64|i128|u8|u16|u32|u64|u128|usize|isize|f32|f64)\b"), 0.75),

        // ---------------------------------------------------------------- Bash
        new("bash", R(@"^#!\s*(?:/usr/bin/env\s+)?(?:bash|sh|zsh|ksh)\b"), 8.0),
        new("bash", R(@"^\s*set\s+-[euxo]"), 2.0),
        new("bash", R(@"^\s*fi\s*$"), 2.5),
        new("bash", R(@"^\s*done\s*$"), 2.5),
        new("bash", R(@"\bfor\s+\w+\s+in\s+[^;]+;\s*do\b"), 3.0),
        new("bash", R(@"\bif\s+\[\[?\s"), 1.5),
        new("bash", R(@"\b(?:then|else|elif|fi|esac|case)\b"), 0.75),
        new("bash", R(@"^\s*(?:echo|export|readonly|local|declare|source|alias|unalias|exec|trap|shift)\s"), 1.5),
        new("bash", R(@"\b(?:grep|sed|awk|chmod|chown|mkdir|rmdir|cp|mv|rm|curl|wget|find|xargs|sort|uniq|wc|head|tail)\s"), 0.75),
        new("bash", R(@"\$\([^()\n]+\)"), 1.0),
        new("bash", R(@"\$\{\w+\}|\$\d+"), 0.5),
        new("bash", R(@"^\s*\w+\s*\(\)\s*\{"), 1.0),

        // ---------------------------------------------------------------- PowerShell
        new("powershell", R(@"\bWrite-(?:Host|Output|Error|Warning|Verbose|Debug|Progress)\b"), 5.0),
        new("powershell", R(@"\b(?:Get|Set|New|Remove|Test|Start|Stop|Restart|Import|Export|Select|Where|ForEach|Sort|Measure|Out|Invoke|ConvertTo|ConvertFrom|Join|Split|Add|Clear|Copy|Move|Rename)-[A-Z]\w+"), 3.0),
        new("powershell", R(@"\$env:\w+"), 3.0),
        new("powershell", R(@"^\s*param\s*\("), 4.0),
        new("powershell", R(@"\[(?:Parameter|CmdletBinding|ValidateSet|ValidateRange|string|int|bool|switch|hashtable)\b"), 2.0),
        new("powershell", R(@"^\s*function\s+[A-Z][a-z]+-[A-Z]"), 3.0),
        new("powershell", R(@"\$(?:true|false|null)\b"), 2.0),
        new("powershell", R(@"@\{|\$_\b"), 1.0),
        new("powershell", R(@"-(?:eq|ne|gt|ge|lt|le|like|notlike|match|notmatch|contains|in|replace|split|join)\b"), 1.5),
        new("powershell", R(@"^\s*#[^\n]*$"), 0.25),

        // ---------------------------------------------------------------- YAML
        new("yaml", R(@"^---\s*$"), 3.0),
        new("yaml", R(@"^\s*[\w.-]+\s*:\s*(?![^\n]*[;{}])(?:\S.*)?$"), 1.5, 6),
        new("yaml", R(@"^\s*-\s+\S"), 1.0, 4),
        new("yaml", R(@"^\s{2,}[\w.-]+\s*:\s*(?![^\n]*[;{}])(?:\S.*)?$"), 1.5, 4),
        new("yaml", R(@"^\s*[\w.-]+\s*:\s*[&*!|>]"), 1.0),
        new("yaml", R(@"^\s*(?:true|false|null|yes|no|on|off|~)\s*$", IgnoreCase), 0.5),

        // ---------------------------------------------------------------- Markdown
        new("markdown", R(@"^#{1,6}\s+\S"), 3.0, 3),
        new("markdown", R(@"^```[\w+-]*\s*$"), 3.0, 2),
        new("markdown", R(@"^\s*[-*+]\s+\S"), 0.75, 4),
        new("markdown", R(@"^\s*\d+\.\s+\S"), 0.75, 2),
        new("markdown", R(@"\[[^\]]+\]\([^)\s]+\)"), 2.0),
        new("markdown", R(@"\*\*[^*\n]+\*\*|__[^_\n]+__"), 1.0),
        new("markdown", R(@"^>\s+\S"), 1.0),
        new("markdown", R(@"~~[^~\n]+~~|`[^`\n]+`"), 0.5),
        new("markdown", R(@"^\|[^\n]*\|[^\n]*$"), 1.0),
        new("markdown", R(@"^-{3,}\s*$|^\*{3,}\s*$"), 0.5)
    ];

    private static readonly Regex CodeStructure = R(@"[{};]|\b(?:function|const|let|var|def|class|public|private|import|return)\b");

    /// <summary>
    /// Returns the most likely language for <paramref name="text"/>, or <c>null</c>
    /// when no language scores high enough or the top two are too close to call.
    /// </summary>
    public static LanguageDefinition? Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var sample = text.Length > MaxSampleLength ? text[..MaxSampleLength] : text;
        if (sample.Length > 0 && sample[0] == '\uFEFF') sample = sample[1..];

        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var signal in Signals)
        {
            int matches;
            try
            {
                matches = signal.Pattern.Matches(sample).Count;
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }

            if (matches == 0) continue;
            var count = Math.Min(matches, signal.MaxMatches);
            scores[signal.LanguageId] = scores.GetValueOrDefault(signal.LanguageId) + signal.Weight * count;
        }

        if (LooksLikeJson(sample)) Add(scores, "json", 8.0);

        // Braces, semicolons and code keywords mean the sample is source code, not YAML.
        if (scores.TryGetValue("yaml", out var yaml) && CodeStructure.IsMatch(sample))
        {
            scores["yaml"] = yaml - 3.0;
        }

        var ranked = scores
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();

        if (ranked.Count == 0) return null;

        var best = ranked[0];
        if (best.Value < MinimumScore) return null;
        if (ranked.Count > 1 && best.Value - ranked[1].Value < MinimumMargin) return null;

        return LanguageCatalog.GetById(best.Key);
    }

    private static void Add(Dictionary<string, double> scores, string languageId, double score) =>
        scores[languageId] = scores.GetValueOrDefault(languageId) + score;

    private static bool LooksLikeJson(string sample)
    {
        var trimmed = sample.TrimStart();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '[')) return false;

        try
        {
            using var document = JsonDocument.Parse(sample, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 64
            });

            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
