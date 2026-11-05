# Changelog

All notable changes to Notepad Pro are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-11-05

### Fixed

- Syntax highlighting now works in C#, HTML, and XML. AvalonEdit compiles generated rules with
  `RegexOptions.IgnorePatternWhitespace`, so an unescaped `#` started a regex comment. That made the
  C# definition invalid (the app silently fell back to plain text) and silently disabled several other
  rules, including CSS hex colours, Rust attributes, Bash shebangs, and Markdown headings.
- Generated definitions now escape every literal `#` and are validated before they reach the editor.

### Added

- A WPF-layer test project that loads every language in every colour scheme, compiles every generated
  regex the same way AvalonEdit does, and verifies real token colours (48 tests; 142 total).

## [1.0.1] - 2026-11-04

### Fixed

- Pasting code into a new untitled document now applies syntax highlighting. The language is
  detected from the pasted content instead of leaving the tab as Plain Text.

### Added

- **Language → Detect from content** (also available from the status-bar language menu) re-detects
  the language of an existing document at any time.
- **Settings → Editor → Detect language when pasting code** enables or disables automatic detection
  for new untitled documents (on by default).
- Content-based detection heuristics for all 21 built-in languages, returning no result rather than
  guessing when the sample is short or ambiguous.
- 26 additional unit tests covering content detection (94 total).

## [1.0.0] - 2026-11-04

First public release.

### Added

- Native Windows 11–style shell with a custom title bar, menu bar, tab strip, editor, and status bar.
- Multi-tab editing with modified indicators, tab context menus, session restore, and
  reopen-closed-tab.
- AvalonEdit-based editor with line numbers, current-line highlight, indentation guides, bracket
  matching, whitespace and line-ending markers, code folding, word wrap, and zoom.
- Runtime-generated syntax highlighting for 21 languages, with extension-based detection and manual
  per-tab overrides.
- Find and replace overlay with match counts, case-sensitive, whole-word, and regular-expression
  modes, highlight-all, and Replace All.
- Full editing command set: duplicate/delete/move lines, toggle comment, indent/outdent, trim
  trailing whitespace, case conversion, and insert date/time.
- Encoding detection and preservation for UTF-8, UTF-8 with BOM, UTF-16 LE/BE, UTF-32, and ANSI;
  CRLF/LF/CR preservation; atomic saves.
- System, Light, and Dark application themes, six editor color schemes, and per-scheme syntax color
  overrides with a color picker.
- Settings, Go to Line, About, Shortcuts, recovery, and confirmation dialogs that follow the active
  theme.
- Background recovery snapshots, unsaved-change prompts, external-change detection, and error
  logging.
- Per-user installer and uninstaller scripts with Start Menu and Open With registration.
- GitHub Actions build/test workflow and tag-driven release workflow.
- 68 unit tests covering the Core layer.

### Known limitations

- No multi-cursor, minimap, split view, or native line spacing.
- x64 only.
- Release binaries are not code-signed.
