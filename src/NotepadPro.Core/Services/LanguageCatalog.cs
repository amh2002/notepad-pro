using NotepadPro.Core.Models;

namespace NotepadPro.Core.Services;

/// <summary>Built-in language catalogue plus extension-based detection.</summary>
public static class LanguageCatalog
{
    public const string PlainTextId = "plaintext";

    public static LanguageDefinition PlainText { get; } = new()
    {
        Id = PlainTextId,
        DisplayName = "Plain Text",
        Extensions = [".txt", ".text", ".log", ".nfo", ".me", ".license"],
        AutoIndent = false
    };

    public static IReadOnlyList<LanguageDefinition> All { get; } = Build();

    private static readonly Dictionary<string, LanguageDefinition> ById =
        All.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, LanguageDefinition> ByExtension = BuildExtensionMap();
    private static readonly Dictionary<string, LanguageDefinition> ByFileName = BuildFileNameMap();

    public static LanguageDefinition GetById(string? id) =>
        id is not null && ById.TryGetValue(id, out var language) ? language : PlainText;

    public static LanguageDefinition DetectFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return PlainText;

        var fileName = Path.GetFileName(path);
        if (ByFileName.TryGetValue(fileName, out var byName)) return byName;

        var extension = Path.GetExtension(path);
        return ByExtension.TryGetValue(extension, out var byExtension) ? byExtension : PlainText;
    }

    private static Dictionary<string, LanguageDefinition> BuildExtensionMap()
    {
        var map = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in All)
        {
            foreach (var extension in language.Extensions)
            {
                map[extension] = language;
            }
        }
        return map;
    }

    private static Dictionary<string, LanguageDefinition> BuildFileNameMap()
    {
        var map = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dockerfile"] = GetById("bash"),
            [".gitignore"] = PlainText,
            [".editorconfig"] = PlainText,
            ["Makefile"] = PlainText,
            ["LICENSE"] = PlainText,
            ["README"] = GetById("markdown"),
            ["CMakeLists.txt"] = PlainText,
            ["package.json"] = GetById("json"),
            ["tsconfig.json"] = GetById("json"),
            ["appsettings.json"] = GetById("json"),
            ["launch.json"] = GetById("json")
        };
        return map;
    }

    private static IReadOnlyList<LanguageDefinition> Build() =>
    [
        PlainText,
        new LanguageDefinition
        {
            Id = "csharp",
            DisplayName = "C#",
            Extensions = [".cs", ".csx"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "abstract", "as", "base", "break", "case", "catch", "checked", "class", "const", "continue",
                "default", "delegate", "do", "else", "enum", "event", "explicit", "extern", "finally", "fixed",
                "for", "foreach", "goto", "if", "implicit", "in", "interface", "internal", "is", "lock", "namespace",
                "new", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "record",
                "ref", "return", "sealed", "sizeof", "stackalloc", "static", "struct", "switch", "this", "throw", "try",
                "typeof", "unchecked", "unsafe", "using", "virtual", "volatile", "while", "async", "await", "yield",
                "partial", "required", "where", "when", "with", "and", "or", "not", "scoped", "file", "global", "nameof"
            ],
            TypeKeywords =
            [
                "bool", "byte", "char", "decimal", "double", "dynamic", "float", "int", "long", "nint", "nuint",
                "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "void", "var", "Span", "Memory",
                "Task", "ValueTask", "List", "Dictionary", "HashSet", "IEnumerable", "IList", "ICollection",
                "IReadOnlyList", "IReadOnlyDictionary", "Action", "Func", "Exception", "Nullable", "Guid", "DateTime",
                "TimeSpan", "Uri", "StringBuilder", "Array", "Enumerable"
            ],
            Constants = ["true", "false", "null", "default", "this", "base"],
            Builtins = ["Console", "Math", "Convert", "Environment", "File", "Directory", "Path", "Encoding", "Regex"]
        },
        new LanguageDefinition
        {
            Id = "c",
            DisplayName = "C",
            Extensions = [".c"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "auto", "break", "case", "const", "continue", "default", "do", "else", "enum", "extern", "for",
                "goto", "if", "inline", "register", "restrict", "return", "sizeof", "static", "struct", "switch",
                "typedef", "union", "volatile", "while", "_Alignas", "_Alignof", "_Atomic", "_Bool", "_Complex",
                "_Generic", "_Imaginary", "_Noreturn", "_Static_assert", "_Thread_local"
            ],
            TypeKeywords =
            [
                "char", "double", "float", "int", "long", "short", "signed", "unsigned", "void", "size_t",
                "int8_t", "int16_t", "int32_t", "int64_t", "uint8_t", "uint16_t", "uint32_t", "uint64_t",
                "intptr_t", "uintptr_t", "FILE", "time_t", "va_list"
            ],
            Constants = ["NULL", "true", "false", "EOF", "stdin", "stdout", "stderr"],
            Builtins = ["printf", "fprintf", "sprintf", "snprintf", "scanf", "malloc", "calloc", "realloc", "free", "memcpy", "memset", "strlen", "strcmp", "strcpy"]
        },
        new LanguageDefinition
        {
            Id = "cpp",
            DisplayName = "C++",
            Extensions = [".cpp", ".cc", ".cxx", ".hpp", ".hxx", ".hh", ".h"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "break", "case", "catch",
                "class", "compl", "concept", "const", "consteval", "constexpr", "constinit", "const_cast", "continue",
                "co_await", "co_return", "co_yield", "decltype", "default", "delete", "do", "dynamic_cast", "else",
                "enum", "explicit", "export", "extern", "false", "final", "for", "friend", "goto", "if", "import",
                "inline", "module", "mutable", "namespace", "new", "noexcept", "not", "not_eq", "nullptr", "operator",
                "or", "or_eq", "override", "private", "protected", "public", "register", "reinterpret_cast", "requires",
                "return", "sizeof", "static", "static_assert", "static_cast", "struct", "switch", "template", "this",
                "thread_local", "throw", "true", "try", "typedef", "typeid", "typename", "union", "using", "virtual",
                "volatile", "while", "xor", "xor_eq"
            ],
            TypeKeywords =
            [
                "bool", "char", "char8_t", "char16_t", "char32_t", "double", "float", "int", "long", "short",
                "signed", "unsigned", "void", "wchar_t", "std", "string", "wstring", "vector", "map", "unordered_map",
                "set", "unordered_set", "pair", "tuple", "unique_ptr", "shared_ptr", "weak_ptr", "optional", "variant",
                "size_t", "ptrdiff_t", "int8_t", "int16_t", "int32_t", "int64_t", "uint8_t", "uint16_t", "uint32_t", "uint64_t"
            ],
            Constants = ["true", "false", "nullptr", "NULL"],
            Builtins = ["cout", "cin", "cerr", "endl", "std", "printf", "malloc", "free"]
        },
        new LanguageDefinition
        {
            Id = "java",
            DisplayName = "Java",
            Extensions = [".java"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "abstract", "assert", "break", "case", "catch", "class", "const", "continue", "default", "do", "else",
                "enum", "extends", "final", "finally", "for", "goto", "if", "implements", "import", "instanceof",
                "interface", "native", "new", "package", "private", "protected", "public", "record", "return", "sealed",
                "static", "strictfp", "super", "switch", "synchronized", "this", "throw", "throws", "transient", "try",
                "var", "volatile", "while", "yield", "permits", "non-sealed"
            ],
            TypeKeywords =
            [
                "boolean", "byte", "char", "double", "float", "int", "long", "short", "void", "String", "Object",
                "Integer", "Long", "Double", "Float", "Boolean", "Character", "List", "ArrayList", "Map", "HashMap",
                "Set", "HashSet", "Optional", "Stream", "Exception", "RuntimeException", "Thread", "Runnable", "Class"
            ],
            Constants = ["true", "false", "null"],
            Builtins = ["System", "Math", "Objects", "Arrays", "Collections", "StringBuilder", "LocalDate", "LocalDateTime"]
        },
        new LanguageDefinition
        {
            Id = "javascript",
            DisplayName = "JavaScript",
            Extensions = [".js", ".mjs", ".cjs", ".jsx"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else",
                "export", "extends", "finally", "for", "function", "if", "import", "in", "instanceof", "let", "new",
                "return", "super", "switch", "this", "throw", "try", "typeof", "var", "void", "while", "with", "yield",
                "async", "await", "of", "from", "as", "static", "get", "set", "target"
            ],
            TypeKeywords =
            [
                "Array", "Object", "String", "Number", "Boolean", "Date", "RegExp", "Promise", "Map", "Set", "WeakMap",
                "WeakSet", "JSON", "Math", "BigInt", "Symbol", "Error", "TypeError", "Function", "Proxy", "Reflect"
            ],
            Constants = ["true", "false", "null", "undefined", "NaN", "Infinity"],
            Builtins = ["console", "window", "document", "globalThis", "setTimeout", "setInterval", "clearTimeout", "fetch", "require", "module", "process"]
        },
        new LanguageDefinition
        {
            Id = "typescript",
            DisplayName = "TypeScript",
            Extensions = [".ts", ".tsx", ".mts", ".cts"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do", "else",
                "export", "extends", "finally", "for", "function", "if", "import", "in", "instanceof", "let", "new",
                "return", "super", "switch", "this", "throw", "try", "typeof", "var", "void", "while", "with", "yield",
                "async", "await", "of", "from", "as", "static", "get", "set", "type", "interface", "enum", "namespace",
                "declare", "abstract", "readonly", "keyof", "infer", "is", "asserts", "satisfies", "override", "implements",
                "public", "private", "protected", "module", "global", "unique"
            ],
            TypeKeywords =
            [
                "any", "unknown", "never", "string", "number", "boolean", "bigint", "symbol", "object", "void",
                "Array", "ReadonlyArray", "Promise", "Map", "Set", "Record", "Partial", "Required", "Readonly", "Pick",
                "Omit", "Exclude", "Extract", "NonNullable", "ReturnType", "Parameters", "Awaited", "Function", "Date"
            ],
            Constants = ["true", "false", "null", "undefined", "NaN", "Infinity"],
            Builtins = ["console", "window", "document", "globalThis", "setTimeout", "setInterval", "fetch", "process"]
        },
        new LanguageDefinition
        {
            Id = "python",
            DisplayName = "Python",
            Extensions = [".py", ".pyw", ".pyi"],
            LineComment = "#",
            FoldByIndentation = true,
            Keywords =
            [
                "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del", "elif", "else",
                "except", "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal", "not",
                "or", "pass", "raise", "return", "try", "while", "with", "yield", "match", "case", "type"
            ],
            TypeKeywords =
            [
                "str", "int", "float", "bool", "bytes", "bytearray", "list", "dict", "set", "frozenset", "tuple",
                "object", "type", "complex", "memoryview", "range"
            ],
            Constants = ["True", "False", "None", "NotImplemented", "Ellipsis", "__name__", "__main__"],
            Builtins =
            [
                "print", "len", "abs", "all", "any", "bin", "bool", "chr", "dict", "dir", "enumerate", "eval", "filter",
                "format", "getattr", "hasattr", "hash", "hex", "id", "input", "isinstance", "issubclass", "iter", "list",
                "map", "max", "min", "next", "object", "oct", "open", "ord", "pow", "range", "repr", "reversed", "round",
                "set", "setattr", "sorted", "str", "sum", "super", "tuple", "type", "vars", "zip", "self", "cls"
            ]
        },
        new LanguageDefinition
        {
            Id = "html",
            DisplayName = "HTML",
            Extensions = [".html", ".htm", ".xhtml"],
            BlockCommentStart = "<!--",
            BlockCommentEnd = "-->",
            Tags =
            [
                "html", "head", "body", "title", "meta", "link", "style", "script", "div", "span", "p", "a", "img",
                "ul", "ol", "li", "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "h1", "h2", "h3",
                "h4", "h5", "h6", "header", "footer", "main", "section", "article", "aside", "nav", "form", "input",
                "button", "select", "option", "textarea", "label", "fieldset", "legend", "br", "hr", "pre", "code",
                "blockquote", "figure", "figcaption", "video", "audio", "source", "canvas", "svg", "template", "slot",
                "dialog", "details", "summary", "iframe", "strong", "em", "b", "i", "u", "small", "mark", "del", "ins"
            ],
            Attributes =
            [
                "class", "id", "style", "src", "href", "alt", "title", "width", "height", "type", "name", "value",
                "placeholder", "disabled", "checked", "selected", "readonly", "required", "target", "rel", "lang",
                "charset", "content", "http-equiv", "role", "aria-label", "aria-hidden", "data-", "action", "method",
                "for", "colspan", "rowspan", "loading", "defer", "async", "integrity", "crossorigin", "tabindex"
            ],
            Constants = ["true", "false", "null", "GET", "POST"],
            AutoIndent = true
        },
        new LanguageDefinition
        {
            Id = "css",
            DisplayName = "CSS",
            Extensions = [".css"],
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Properties =
            [
                "align-content", "align-items", "align-self", "animation", "background", "background-color", "background-image",
                "background-position", "background-repeat", "background-size", "border", "border-bottom", "border-color",
                "border-left", "border-radius", "border-right", "border-style", "border-top", "border-width", "box-shadow",
                "box-sizing", "color", "column-gap", "content", "cursor", "display", "filter", "flex", "flex-basis",
                "flex-direction", "flex-grow", "flex-shrink", "flex-wrap", "font", "font-family", "font-size", "font-style",
                "font-weight", "gap", "grid", "grid-area", "grid-column", "grid-row", "grid-template-columns",
                "grid-template-rows", "height", "justify-content", "left", "letter-spacing", "line-height", "margin",
                "margin-bottom", "margin-left", "margin-right", "margin-top", "max-height", "max-width", "min-height",
                "min-width", "object-fit", "opacity", "order", "outline", "overflow", "overflow-x", "overflow-y", "padding",
                "padding-bottom", "padding-left", "padding-right", "padding-top", "pointer-events", "position", "right",
                "row-gap", "text-align", "text-decoration", "text-overflow", "text-transform", "top", "transform",
                "transition", "user-select", "vertical-align", "visibility", "white-space", "width", "word-break", "z-index"
            ],
            TypeKeywords =
            [
                "absolute", "relative", "fixed", "sticky", "static", "block", "inline", "inline-block", "flex", "grid",
                "none", "auto", "hidden", "visible", "solid", "dashed", "dotted", "transparent", "inherit", "initial",
                "unset", "center", "start", "end", "space-between", "space-around", "space-evenly", "column", "row",
                "wrap", "nowrap", "bold", "normal", "italic", "uppercase", "lowercase", "pointer", "border-box",
                "content-box", "ease", "ease-in", "ease-out", "linear", "infinite", "important"
            ],
            Constants = ["0", "100%", "px", "rem", "em", "vh", "vw", "deg", "ms", "s"],
            Builtins = ["var", "calc", "url", "rgb", "rgba", "hsl", "hsla", "linear-gradient", "radial-gradient", "translate", "rotate", "scale"]
        },
        new LanguageDefinition
        {
            Id = "scss",
            DisplayName = "SCSS",
            Extensions = [".scss", ".sass"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "@use", "@forward", "@import", "@mixin", "@include", "@extend", "@function", "@return", "@if", "@else",
                "@each", "@for", "@while", "@debug", "@warn", "@error", "@at-root", "@media", "@supports", "@keyframes",
                "@font-face", "@content"
            ],
            TypeKeywords =
            [
                "absolute", "relative", "fixed", "sticky", "static", "block", "inline", "inline-block", "flex", "grid",
                "none", "auto", "hidden", "visible", "solid", "transparent", "inherit", "center", "space-between",
                "column", "row", "wrap", "bold", "normal", "italic", "pointer", "border-box", "content-box"
            ],
            Properties = BuildCssProperties(),
            Constants = ["true", "false", "null", "important"],
            Builtins = ["var", "calc", "url", "rgb", "rgba", "hsl", "hsla", "map-get", "map-merge", "lighten", "darken", "mix"]
        },
        new LanguageDefinition
        {
            Id = "json",
            DisplayName = "JSON",
            Extensions = [".json", ".jsonc", ".geojson", ".webmanifest"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Constants = ["true", "false", "null"],
            AutoIndent = true
        },
        new LanguageDefinition
        {
            Id = "xml",
            DisplayName = "XML",
            Extensions = [".xml", ".xsd", ".xsl", ".xslt", ".svg", ".xaml", ".config", ".csproj", ".props", ".targets", ".resx", ".plist"],
            BlockCommentStart = "<!--",
            BlockCommentEnd = "-->",
            Keywords = ["CDATA", "DOCTYPE"],
            AutoIndent = true
        },
        new LanguageDefinition
        {
            Id = "sql",
            DisplayName = "SQL",
            Extensions = [".sql", ".ddl", ".dml"],
            LineComment = "--",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            CaseInsensitive = true,
            Keywords =
            [
                "add", "all", "alter", "and", "any", "as", "asc", "backup", "begin", "between", "by", "case", "check",
                "column", "commit", "constraint", "create", "database", "default", "delete", "desc", "distinct", "drop",
                "else", "end", "exec", "exists", "foreign", "from", "full", "group", "having", "if", "in", "index",
                "inner", "insert", "into", "is", "join", "key", "left", "like", "limit", "not", "null", "on", "or",
                "order", "outer", "primary", "procedure", "right", "rollback", "rownum", "select", "set", "table", "then",
                "top", "transaction", "trigger", "truncate", "union", "unique", "update", "values", "view", "when", "where",
                "with", "offset", "fetch", "over", "partition", "returning", "merge", "using", "declare"
            ],
            TypeKeywords =
            [
                "bigint", "bit", "blob", "boolean", "char", "date", "datetime", "decimal", "double", "float", "int",
                "integer", "json", "numeric", "nvarchar", "real", "smallint", "text", "time", "timestamp", "tinyint",
                "uniqueidentifier", "varchar", "xml", "uuid", "serial", "bigserial"
            ],
            Constants = ["true", "false", "null", "getdate", "sysdate", "current_timestamp", "now"],
            Builtins =
            [
                "count", "sum", "avg", "min", "max", "coalesce", "isnull", "nullif", "cast", "convert", "substring",
                "length", "upper", "lower", "trim", "replace", "round", "abs", "dateadd", "datediff", "format", "row_number"
            ]
        },
        new LanguageDefinition
        {
            Id = "php",
            DisplayName = "PHP",
            Extensions = [".php", ".phtml", ".inc"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "abstract", "and", "array", "as", "break", "callable", "case", "catch", "class", "clone", "const",
                "continue", "declare", "default", "do", "else", "elseif", "enddeclare", "endfor", "endforeach", "endif",
                "endswitch", "endwhile", "enum", "extends", "final", "finally", "fn", "for", "foreach", "function",
                "global", "goto", "if", "implements", "include", "include_once", "instanceof", "insteadof", "interface",
                "isset", "list", "match", "namespace", "new", "or", "print", "private", "protected", "public", "readonly",
                "require", "require_once", "return", "static", "switch", "throw", "trait", "try", "unset", "use", "var",
                "while", "xor", "yield", "echo", "exit", "die", "empty"
            ],
            TypeKeywords = ["bool", "boolean", "int", "integer", "float", "double", "string", "array", "object", "mixed", "void", "null", "callable", "iterable", "never", "self", "parent", "static"],
            Constants = ["true", "false", "null", "PHP_EOL", "PHP_INT_MAX", "__DIR__", "__FILE__", "__LINE__", "__CLASS__", "__FUNCTION__"],
            Builtins =
            [
                "strlen", "str_replace", "str_contains", "str_starts_with", "str_ends_with", "substr", "trim", "explode",
                "implode", "count", "array_map", "array_filter", "array_merge", "array_keys", "array_values", "in_array",
                "json_encode", "json_decode", "preg_match", "preg_replace", "file_get_contents", "file_put_contents",
                "var_dump", "print_r", "sprintf", "printf", "htmlspecialchars", "date"
            ]
        },
        new LanguageDefinition
        {
            Id = "go",
            DisplayName = "Go",
            Extensions = [".go"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough", "for", "func",
                "go", "goto", "if", "import", "interface", "map", "package", "range", "return", "select", "struct",
                "switch", "type", "var"
            ],
            TypeKeywords =
            [
                "bool", "byte", "complex64", "complex128", "error", "float32", "float64", "int", "int8", "int16", "int32",
                "int64", "rune", "string", "uint", "uint8", "uint16", "uint32", "uint64", "uintptr", "any", "comparable"
            ],
            Constants = ["true", "false", "nil", "iota"],
            Builtins =
            [
                "append", "cap", "close", "complex", "copy", "delete", "imag", "len", "make", "new", "panic", "print",
                "println", "real", "recover", "fmt", "Println", "Printf", "Sprintf", "Errorf", "context", "errors", "strings", "strconv", "time", "http"
            ]
        },
        new LanguageDefinition
        {
            Id = "rust",
            DisplayName = "Rust",
            Extensions = [".rs"],
            LineComment = "//",
            BlockCommentStart = "/*",
            BlockCommentEnd = "*/",
            Keywords =
            [
                "as", "async", "await", "break", "const", "continue", "crate", "dyn", "else", "enum", "extern", "false",
                "fn", "for", "if", "impl", "in", "let", "loop", "match", "mod", "move", "mut", "pub", "ref", "return",
                "self", "Self", "static", "struct", "super", "trait", "true", "type", "unsafe", "use", "where", "while",
                "abstract", "become", "box", "do", "final", "macro", "override", "priv", "typeof", "unsized", "virtual",
                "yield", "try"
            ],
            TypeKeywords =
            [
                "bool", "char", "f32", "f64", "i8", "i16", "i32", "i64", "i128", "isize", "str", "u8", "u16", "u32",
                "u64", "u128", "usize", "String", "Vec", "Option", "Result", "Box", "Rc", "Arc", "HashMap", "HashSet",
                "BTreeMap", "BTreeSet", "Cow", "Path", "PathBuf", "Duration", "Instant"
            ],
            Constants = ["true", "false", "None", "Some", "Ok", "Err"],
            Builtins =
            [
                "println", "print", "eprintln", "eprint", "format", "vec", "panic", "assert", "assert_eq", "assert_ne",
                "dbg", "write", "writeln", "todo", "unimplemented", "unreachable", "drop", "clone", "unwrap", "expect"
            ]
        },
        new LanguageDefinition
        {
            Id = "bash",
            DisplayName = "Shell (Bash)",
            Extensions = [".sh", ".bash", ".zsh", ".ksh"],
            LineComment = "#",
            FoldByIndentation = true,
            Keywords =
            [
                "if", "then", "else", "elif", "fi", "for", "while", "until", "do", "done", "case", "esac", "in",
                "function", "select", "time", "coproc", "return", "exit", "break", "continue", "local", "export",
                "readonly", "declare", "typeset", "unset", "shift", "source", "alias", "unalias", "eval", "exec",
                "trap", "wait", "set", "shopt"
            ],
            TypeKeywords = ["string", "int", "array"],
            Constants = ["true", "false"],
            Builtins =
            [
                "echo", "printf", "read", "cd", "pwd", "ls", "cat", "grep", "sed", "awk", "cut", "sort", "uniq", "wc",
                "find", "xargs", "tr", "head", "tail", "mkdir", "rmdir", "rm", "cp", "mv", "touch", "chmod", "chown",
                "test", "sleep", "kill", "ps", "env", "date", "basename", "dirname", "realpath", "curl", "wget", "git"
            ]
        },
        new LanguageDefinition
        {
            Id = "powershell",
            DisplayName = "PowerShell",
            Extensions = [".ps1", ".psm1", ".psd1"],
            LineComment = "#",
            BlockCommentStart = "<#",
            BlockCommentEnd = "#>",
            CaseInsensitive = true,
            FoldByIndentation = true,
            Keywords =
            [
                "begin", "break", "catch", "class", "continue", "data", "define", "do", "dynamicparam", "else", "elseif",
                "end", "enum", "exit", "filter", "finally", "for", "foreach", "from", "function", "if", "in", "param",
                "process", "return", "switch", "throw", "trap", "try", "until", "using", "var", "while", "workflow",
                "hidden", "static"
            ],
            TypeKeywords =
            [
                "array", "bool", "byte", "char", "datetime", "decimal", "double", "float", "guid", "hashtable", "int",
                "int16", "int32", "int64", "long", "object", "psobject", "regex", "scriptblock", "single", "string",
                "switch", "type", "uint16", "uint32", "uint64", "ulong", "void", "xml"
            ],
            Constants = ["true", "false", "null", "_", "args", "input", "this", "psitem"],
            Builtins =
            [
                "Write-Host", "Write-Output", "Write-Error", "Write-Warning", "Get-ChildItem", "Get-Content", "Set-Content",
                "Get-Item", "Remove-Item", "Copy-Item", "Move-Item", "Test-Path", "New-Item", "Select-Object", "Where-Object",
                "ForEach-Object", "Sort-Object", "Measure-Object", "Get-Process", "Start-Process", "Stop-Process",
                "Invoke-Command", "Invoke-WebRequest", "Get-Command", "Get-Help", "Out-File", "Out-String", "ConvertTo-Json",
                "ConvertFrom-Json"
            ]
        },
        new LanguageDefinition
        {
            Id = "yaml",
            DisplayName = "YAML",
            Extensions = [".yml", ".yaml"],
            LineComment = "#",
            FoldByIndentation = true,
            Keywords = ["---", "..."],
            Constants = ["true", "false", "null", "yes", "no", "on", "off", "~"],
            AutoIndent = true
        },
        new LanguageDefinition
        {
            Id = "markdown",
            DisplayName = "Markdown",
            Extensions = [".md", ".markdown", ".mdown", ".mkd"],
            FoldByIndentation = true,
            Keywords = [],
            AutoIndent = false
        }
    ];

    private static string[] BuildCssProperties()
    {
        // Reuse the CSS property list once, then append SCSS-specific properties.
        var baseProperties = new LanguageDefinition
        {
            Id = "css",
            DisplayName = "CSS",
            Extensions = [".css"],
            Properties =
            [
                "align-content", "align-items", "align-self", "animation", "background", "background-color", "background-image",
                "background-position", "background-repeat", "background-size", "border", "border-bottom", "border-color",
                "border-left", "border-radius", "border-right", "border-style", "border-top", "border-width", "box-shadow",
                "box-sizing", "color", "column-gap", "content", "cursor", "display", "filter", "flex", "flex-basis",
                "flex-direction", "flex-grow", "flex-shrink", "flex-wrap", "font", "font-family", "font-size", "font-style",
                "font-weight", "gap", "grid", "grid-area", "grid-column", "grid-row", "grid-template-columns",
                "grid-template-rows", "height", "justify-content", "left", "letter-spacing", "line-height", "margin",
                "margin-bottom", "margin-left", "margin-right", "margin-top", "max-height", "max-width", "min-height",
                "min-width", "object-fit", "opacity", "order", "outline", "overflow", "overflow-x", "overflow-y", "padding",
                "padding-bottom", "padding-left", "padding-right", "padding-top", "pointer-events", "position", "right",
                "row-gap", "text-align", "text-decoration", "text-overflow", "text-transform", "top", "transform",
                "transition", "user-select", "vertical-align", "visibility", "white-space", "width", "word-break", "z-index"
            ]
        };
        return [.. baseProperties.Properties, "fill", "stroke", "stroke-width", "accent-color", "aspect-ratio"];
    }
}
