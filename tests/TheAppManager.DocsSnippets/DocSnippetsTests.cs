using Shouldly;

namespace TheAppManager.DocsSnippets;

/// <summary>Every C# block of the documentation mirrors a compiled region of <c>Snippets/*.cs</c>.</summary>
public sealed class DocSnippetsTests
{
    private static readonly Dictionary<string, string> Inline = new() { ["hello"] = "var x = 1;\nConsole.WriteLine(x);" };

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs/migration-v2-to-v3.md")]
    public void Document_snippets_match_compiled_sources(string relativePath)
    {
        var root = RepositoryRoot();
        var snippets = SnippetChecker.LoadSnippets(Path.Combine(root, "tests", "TheAppManager.DocsSnippets", "Snippets"));
        var markdown = File.ReadAllText(Path.Combine(root, relativePath));

        var failures = SnippetChecker.Check(markdown, relativePath, snippets);

        failures.ShouldBeEmpty(string.Join("\n\n", failures));
    }

    [Fact]
    public void Matching_block_passes_whatever_the_indentation_and_line_endings()
    {
        var markdown = "- item\r\n\r\n  <!-- snippet: hello -->\r\n  ```csharp\r\n  var x = 1;  \r\n  Console.WriteLine(x);\r\n  ```\r\n";

        SnippetChecker.Check(markdown, "doc.md", Inline).ShouldBeEmpty();
    }

    [Fact]
    public void Drifted_block_is_reported()
    {
        const string markdown = "<!-- snippet: hello -->\n```csharp\nvar x = 2;\nConsole.WriteLine(x);\n```\n";

        var failure = SnippetChecker.Check(markdown, "doc.md", Inline).ShouldHaveSingleItem();

        failure.ShouldContain("doc.md:2");
        failure.ShouldContain("'hello'");
    }

    [Fact]
    public void Unmarked_csharp_block_is_reported()
    {
        const string markdown = "Text.\n\n```cs\nvar x = 1;\n```\n\n```bash\ndotnet test\n```\n";

        SnippetChecker.Check(markdown, "doc.md", Inline).ShouldHaveSingleItem().ShouldContain("no <!-- snippet: name --> marker");
    }

    [Fact]
    public void Unknown_snippet_name_is_reported()
    {
        const string markdown = "<!-- snippet: missing -->\n```c#\nvar x = 1;\n```\n";

        SnippetChecker.Check(markdown, "doc.md", Inline).ShouldHaveSingleItem().ShouldContain("'missing'");
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TheAppManager.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("TheAppManager.sln not found above " + AppContext.BaseDirectory);
    }
}
