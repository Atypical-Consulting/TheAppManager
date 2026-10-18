using System.Text.RegularExpressions;

namespace TheAppManager.DocsSnippets;

/// <summary>
/// Compares the C# blocks of a markdown document with the compiled snippet regions of <c>Snippets/*.cs</c>.
/// A C# block must be preceded by <c>&lt;!-- snippet: name --&gt;</c>, and its body must equal the
/// <c>// begin-snippet: name</c> … <c>// end-snippet</c> region (MarkdownSnippets syntax).
/// </summary>
internal static partial class SnippetChecker
{
    private static readonly HashSet<string> CSharpInfo = new(StringComparer.OrdinalIgnoreCase) { "csharp", "cs", "c#" };

    /// <summary>Returns one message per failure; an empty list means the document passes.</summary>
    public static IReadOnlyList<string> Check(string markdown, string documentName, IReadOnlyDictionary<string, string> snippets)
    {
        var failures = new List<string>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var open = Fence().Match(lines[i]);
            if (!open.Success)
            {
                continue;
            }

            var fence = open.Groups["fence"].Value;
            var info = open.Groups["info"].Value.Trim().Split(' ', 2)[0];
            var start = i;
            var body = new List<string>();
            for (i++; i < lines.Length && !IsClosingFence(lines[i], fence); i++)
            {
                body.Add(lines[i]);
            }

            if (!CSharpInfo.Contains(info))
            {
                continue;
            }

            var where = $"{documentName}:{start + 1}";
            var name = PrecedingMarker(lines, start);
            if (name is null)
            {
                failures.Add($"{where}: C# block has no <!-- snippet: name --> marker on the line before it.");
            }
            else if (!snippets.TryGetValue(name, out var expected))
            {
                failures.Add($"{where}: snippet '{name}' has no // begin-snippet: {name} region in Snippets/*.cs.");
            }
            else if (Normalize(string.Join('\n', body)) is var actual && actual != expected)
            {
                failures.Add($"{where}: snippet '{name}' drifted from its compiled source.\n" +
                             $"--- document ---\n{actual}\n--- Snippets/*.cs ---\n{expected}");
            }
        }

        return failures;
    }

    /// <summary>Reads every <c>// begin-snippet: name</c> region of the <c>*.cs</c> files under <paramref name="directory"/>.</summary>
    public static IReadOnlyDictionary<string, string> LoadSnippets(string directory)
    {
        var snippets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            string? name = null;
            var body = new List<string>();
            foreach (var line in File.ReadAllText(file).Replace("\r\n", "\n").Split('\n'))
            {
                var begin = Begin().Match(line);
                if (begin.Success)
                {
                    name = begin.Groups["name"].Value;
                    body.Clear();
                }
                else if (name is not null && line.Trim() == "// end-snippet")
                {
                    if (!snippets.TryAdd(name, Normalize(string.Join('\n', body))))
                    {
                        throw new InvalidOperationException($"Snippet '{name}' is defined twice ({file}).");
                    }

                    name = null;
                }
                else if (name is not null)
                {
                    body.Add(line);
                }
            }

            if (name is not null)
            {
                throw new InvalidOperationException($"Snippet '{name}' in {file} has no // end-snippet.");
            }
        }

        return snippets;
    }

    /// <summary>Trims trailing whitespace per line, the common indentation and the surrounding blank lines.</summary>
    internal static string Normalize(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);

        var indent = lines.Where(l => l.Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join('\n', lines.Select(l => l.Length >= indent ? l[indent..] : l));
    }

    private static bool IsClosingFence(string line, string fence)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= fence.Length && trimmed.All(c => c == fence[0]);
    }

    private static string? PrecedingMarker(string[] lines, int fenceLine)
    {
        for (var j = fenceLine - 1; j >= 0; j--)
        {
            if (lines[j].Trim().Length == 0)
            {
                continue;
            }

            var marker = Marker().Match(lines[j]);
            return marker.Success ? marker.Groups["name"].Value : null;
        }

        return null;
    }

    [GeneratedRegex(@"^\s*(?<fence>`{3,}|~{3,})(?<info>[^`]*)$")]
    private static partial Regex Fence();

    [GeneratedRegex(@"^\s*<!--\s*snippet:\s*(?<name>[\w.-]+)\s*-->\s*$")]
    private static partial Regex Marker();

    [GeneratedRegex(@"^\s*//\s*begin-snippet:\s*(?<name>[\w.-]+)\s*$")]
    private static partial Regex Begin();
}
