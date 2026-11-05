using NotepadPro.Core.Models;
using NotepadPro.Core.Services;

namespace NotepadPro.Core.Tests;

public sealed class LanguageAndSearchTests
{
    [Theory]
    [InlineData("file.cs", "csharp")]
    [InlineData("FILE.CS", "csharp")]
    [InlineData("main.cpp", "cpp")]
    [InlineData("app.tsx", "typescript")]
    [InlineData("index.html", "html")]
    [InlineData("style.scss", "scss")]
    [InlineData("config.yaml", "yaml")]
    [InlineData("script.ps1", "powershell")]
    [InlineData("notes.txt", "plaintext")]
    [InlineData("unknown.zzz", "plaintext")]
    [InlineData(null, "plaintext")]
    public void Extension_Detection_Works(string? path, string expected)
        => Assert.Equal(expected, LanguageCatalog.DetectFromPath(path).Id);

    [Theory]
    [InlineData("csharp")]
    [InlineData("c")]
    [InlineData("cpp")]
    [InlineData("java")]
    [InlineData("javascript")]
    [InlineData("typescript")]
    [InlineData("python")]
    [InlineData("html")]
    [InlineData("css")]
    [InlineData("scss")]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("sql")]
    [InlineData("php")]
    [InlineData("go")]
    [InlineData("rust")]
    [InlineData("bash")]
    [InlineData("powershell")]
    [InlineData("yaml")]
    [InlineData("markdown")]
    [InlineData("plaintext")]
    public void Required_Languages_Exist(string id) => Assert.Equal(id, LanguageCatalog.GetById(id).Id);

    [Fact]
    public void Unknown_Language_Falls_Back_To_Plain_Text()
        => Assert.Equal(LanguageCatalog.PlainTextId, LanguageCatalog.GetById("not-a-language").Id);

    public static IEnumerable<object[]> LanguageSamples()
    {
        yield return ["csharp", """
            using System;

            namespace ColorCheck;

            public sealed class Greeter
            {
                private const string Greeting = "Hello, Notepad Pro!";
                private readonly int _count = 42;

                // This comment should be green.
                public void SayHello(string name)
                {
                    if (string.IsNullOrWhiteSpace(name)) return;
                    Console.WriteLine($"{Greeting} {name} ({_count})");
                }
            }
            """];

        yield return ["c", """
            #include <stdio.h>

            int main(void)
            {
                printf("Hello, world!\n");
                return 0;
            }
            """];

        yield return ["cpp", """
            #include <iostream>
            #include <vector>
            using namespace std;

            int main()
            {
                vector<int> numbers = { 1, 2, 3 };
                int sum = 0;
                for (int n : numbers) sum += n;
                cout << "Sum: " << sum << endl;
                return 0;
            }
            """];

        yield return ["java", """
            import java.util.ArrayList;
            import java.util.List;

            public class Main
            {
                public static void main(String[] args)
                {
                    List<String> names = new ArrayList<>();
                    for (String name : names)
                    {
                        System.out.println(name);
                    }
                }
            }
            """];

        yield return ["javascript", """
            const express = require('express');
            const app = express();

            app.get('/', (req, res) => {
              res.send('Hello, world!');
            });

            app.listen(3000, () => console.log('Server running'));
            """];

        yield return ["typescript", """
            interface User {
              id: number;
              name: string;
            }

            async function fetchUser(id: number): Promise<User> {
              const response = await fetch(`/api/users/${id}`);
              return response.json() as Promise<User>;
            }
            """];

        yield return ["python", """
            # Greet the user
            def greet(name):
                if not name:
                    return "Hello, stranger!"
                return f"Hello, {name}!"

            if __name__ == "__main__":
                print(greet("World"))
            """];

        yield return ["html", """
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>Notepad Pro</title>
            </head>
            <body>
              <div class="app">Hello</div>
            </body>
            </html>
            """];

        yield return ["css", """
            :root {
              --primary: #0078d4;
            }

            .button {
              background-color: var(--primary);
              color: white;
              padding: 8px 16px;
              border-radius: 4px;
            }

            .button:hover {
              background-color: #106ebe;
            }
            """];

        yield return ["scss", """
            $primary: #0078d4;

            @mixin button-style($bg) {
              background: $bg;
              border-radius: 4px;
            }

            .button {
              @include button-style($primary);

              &:hover {
                background: darken($primary, 10%);
              }
            }
            """];

        yield return ["json", """
            {
              "name": "Ada",
              "age": 36,
              "active": true,
              "roles": ["admin", "editor"]
            }
            """];

        yield return ["xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="Theme" value="Dark" />
              </appSettings>
            </configuration>
            """];

        yield return ["sql", """
            SELECT u.Id, u.Name, COUNT(o.Id) AS OrderCount
            FROM Users u
            LEFT JOIN Orders o ON o.UserId = u.Id
            WHERE u.IsActive = 1
            GROUP BY u.Id, u.Name
            ORDER BY OrderCount DESC;
            """];

        yield return ["php", """
            <?php
            declare(strict_types=1);

            function greet(string $name): string {
                return "Hello, " . htmlspecialchars($name);
            }

            $users = ['Ada', 'Grace'];
            foreach ($users as $user) {
                echo greet($user) . PHP_EOL;
            }
            """];

        yield return ["go", """
            package main

            import (
                "fmt"
            )

            func main() {
                names := []string{"Ada", "Grace"}
                for _, name := range names {
                    fmt.Printf("Hello, %s!\n", name)
                }
            }
            """];

        yield return ["rust", """
            use std::collections::HashMap;

            #[derive(Debug)]
            struct User {
                name: String,
                age: u32,
            }

            fn main() {
                let mut users = HashMap::new();
                users.insert("ada", 36);
                println!("{:?}", users);
            }
            """];

        yield return ["bash", """
            #!/usr/bin/env bash
            set -euo pipefail

            for file in *.txt; do
              echo "Processing $file"
              wc -l "$file"
            done
            """];

        yield return ["powershell", """
            param(
                [Parameter(Mandatory = $true)]
                [string]$Path
            )

            Get-ChildItem -Path $Path -Filter *.log |
                Where-Object { $_.Length -gt 0 } |
                ForEach-Object {
                    Write-Host "Found $($_.FullName)"
                }
            """];

        yield return ["yaml", """
            name: Build

            on:
              push:
                branches: [main]

            jobs:
              build:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - name: Set up .NET
                    uses: actions/setup-dotnet@v4
            """];

        yield return ["markdown", """
            # Notepad Pro

            A fast, native editor for Windows.

            ## Features

            - Multi-tab editing
            - Syntax highlighting for 21 languages
            - **Recovery** snapshots

            [Download](https://example.com)
            """];
    }

    [Theory]
    [MemberData(nameof(LanguageSamples))]
    public void Content_Detection_Identifies_Language(string expected, string text)
        => Assert.Equal(expected, LanguageDetector.Detect(text)?.Id);

    [Fact]
    public void Content_Detection_Returns_Null_For_Plain_Prose()
    {
        const string prose = """
            The quick brown fox jumps over the lazy dog. This is just a normal paragraph of
            text with no code in it. We should not try to highlight it as a programming
            language just because it contains words like select, class, or return.
            """;

        Assert.Null(LanguageDetector.Detect(prose));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n\t")]
    [InlineData("public class Foo { }")]
    public void Content_Detection_Returns_Null_When_Unsure(string? text)
        => Assert.Null(LanguageDetector.Detect(text));

    [Fact]
    public void Content_Detection_Only_Inspects_A_Bounded_Sample()
    {
        var padding = new string(' ', LanguageDetector.MaxSampleLength);
        Assert.Null(LanguageDetector.Detect(padding + "Console.WriteLine(42);"));
    }

    [Fact]
    public void Search_Is_Case_Insensitive_By_Default()
    {
        var regex = SearchEngine.TryCreateRegex("hello", new SearchOptions(), out _);
        Assert.NotNull(regex);
        var matches = SearchEngine.FindAll("Hello hello HELLO", regex);
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void Search_Respects_Case_And_Whole_Word()
    {
        var options = new SearchOptions { MatchCase = true, WholeWord = true };
        var regex = SearchEngine.TryCreateRegex("cat", options, out _)!;
        var matches = SearchEngine.FindAll("cat Cat concatenate cat.", regex);
        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.Equal("cat", "cat Cat concatenate cat.".Substring(m.Start, m.Length)));
    }

    [Fact]
    public void Regex_Search_Works()
    {
        var options = new SearchOptions { UseRegex = true };
        var regex = SearchEngine.TryCreateRegex(@"\b\d{3}-\d{2}-\d{4}\b", options, out _)!;
        var matches = SearchEngine.FindAll("SSN 123-45-6789, other 12-34-5678", regex);
        Assert.Single(matches);
        Assert.Equal(4, matches[0].Start);
    }

    [Fact]
    public void Invalid_Regex_Returns_Error()
    {
        var regex = SearchEngine.TryCreateRegex("(", new SearchOptions { UseRegex = true }, out var error);
        Assert.Null(regex);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void FindNext_Wraps_And_FindPrevious_Wraps()
    {
        var regex = SearchEngine.TryCreateRegex("x", new SearchOptions(), out _)!;
        Assert.Equal(6, SearchEngine.FindNext("a x a x", regex, 5));
        Assert.Equal(2, SearchEngine.FindNext("a x a x", regex, 5, forward: false));
        Assert.Equal(2, SearchEngine.FindNext("a x a x", regex, 0));
        Assert.Equal(6, SearchEngine.FindNext("a x a x", regex, 99, forward: false));
    }

    [Fact]
    public void Zero_Length_Regex_Does_Not_Hang()
    {
        var regex = SearchEngine.TryCreateRegex("^", new SearchOptions { UseRegex = true }, out _)!;
        var matches = SearchEngine.FindAll("a\nb\nc", regex);
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void LineEnding_Normalization_Handles_All_Styles()
    {
        Assert.Equal("a\r\nb\r\nc\r\n", LineEndingKinds.Normalize("a\rb\nc\r\n", LineEndingKind.Windows));
        Assert.Equal("a\nb\nc\n", LineEndingKinds.Normalize("a\rb\nc\r\n", LineEndingKind.Unix));
        Assert.Equal("a\rb\rc\r", LineEndingKinds.Normalize("a\rb\nc\r\n", LineEndingKind.Macintosh));
    }
}
