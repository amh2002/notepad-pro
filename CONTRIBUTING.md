# Contributing to Notepad Pro

Thanks for taking the time to improve Notepad Pro. This document covers the
basics: building the project, running the tests, and submitting changes.

## Before you start

- Search the [issue tracker](https://github.com/amh2002/notepad-pro/issues) to
  avoid duplicate work.
- For larger features, open an issue first so the approach can be discussed.
- By contributing, you agree that your contributions are licensed under the
  [MIT License](LICENSE).

## Requirements

- Windows 10 or 11 (x64)
- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer
- Git

## Build and test

```powershell
dotnet restore NotepadPro.sln
dotnet build NotepadPro.sln -c Release
dotnet test NotepadPro.sln -c Release
```

Run the app from source:

```powershell
dotnet run --project src\NotepadPro\NotepadPro.csproj
```

Run a single test class:

```powershell
dotnet test tests\NotepadPro.Core.Tests\NotepadPro.Core.Tests.csproj `
  --filter "FullyQualifiedName~FileServiceTests"
```

## Project structure

| Project | Purpose |
| --- | --- |
| `src/NotepadPro.Core` | Platform-neutral models, file I/O, settings, recovery, search, languages |
| `src/NotepadPro` | WPF shell, editor host, themes, dialogs, view models |
| `tests/NotepadPro.Core.Tests` | xUnit tests for the Core layer |

Keep UI code free of business logic when possible, and add tests for new Core
behavior. Core must not reference WPF.

## Code style

- Follow the style of the surrounding code (file-scoped namespaces, nullable
  reference types, `_camelCase` private fields, expression-bodied members where
  they read well).
- Keep user-facing strings clear and sentence-cased.
- No placeholder buttons, no `TODO` stubs, and no dead menu items. Every
  command should do something real.
- Preserve data-safety behavior: atomic writes, unsaved-change prompts, and
  recovery snapshots must never regress.

## Pull requests

1. Fork the repository and create a topic branch.
2. Make focused commits with descriptive messages.
3. Ensure `dotnet build -c Release` reports zero warnings and all tests pass.
4. Verify the change manually in the running app (including Light/Dark mode
   when the change touches the UI).
5. Open a pull request describing the problem, the solution, and how you tested
   it.

## Reporting bugs

Include:

- Windows version and Notepad Pro version (Help → About)
- Steps to reproduce
- Expected and actual behavior
- A sample file when the issue involves encoding, line endings, or syntax
  highlighting
- The contents of `%LOCALAPPDATA%\NotepadPro\error.log`, if it exists
