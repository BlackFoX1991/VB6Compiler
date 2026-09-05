using System.Text.RegularExpressions;

namespace VB6.Compiler.Tests;

/// <summary>
/// The anchors that <c>build.ps1 -UpdateVerificationDocs</c> writes into.
///
/// Measured numbers used to be copied into three documents by hand, and they aged there unnoticed:
/// 1698 survived as a "test count" long after it had become the sum of a standard run and a
/// separate x86 run. The switch now writes those passages from the run report, and it can only do
/// that where a begin/end marker pair says where the generated text belongs.
///
/// A marker is therefore load-bearing. Delete one and the numbers silently stop being updated
/// while still looking freshly measured -- which is exactly the failure the switch exists to end.
/// These tests fail the moment an anchor goes missing, unbalanced or duplicated.
/// </summary>
[TestClass]
public sealed class VerificationDocumentTests
{
    /// <summary>Every region the update switch produces, and the document that must carry it.</summary>
    private static readonly (string Document, string Region)[] RequiredRegions =
    [
        ("docs/ROADMAP.md", "roadmap-measurements"),
        ("docs/ROADMAP.md", "roadmap-matrix"),
        ("README.md", "readme-status-matrix"),
        ("README.md", "readme-measurements"),
        ("README.md", "readme-matrix"),
        ("CLAUDE.md", "claude-matrix"),
        ("CLAUDE.md", "claude-measurements"),
    ];

    [TestMethod]
    public void Documents_CarryEveryGeneratedRegionExactlyOnce()
    {
        var root = CompatibilityMatrix.FindRepositoryRoot();
        var problems = new List<string>();

        foreach (var (document, region) in RequiredRegions)
        {
            var text = File.ReadAllText(Path.Combine(root, document.Replace('/', Path.DirectorySeparatorChar)));
            var begins = Occurrences(text, $"<!-- verification:{region}:begin -->");
            var ends = Occurrences(text, $"<!-- verification:{region}:end -->");

            if (begins != 1 || ends != 1)
            {
                problems.Add($"{document}: '{region}' has {begins} begin and {ends} end marker(s), expected one of each");
            }
        }

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void Documents_DeclareNoRegionTheUpdateSwitchDoesNotWrite()
    {
        var root = CompatibilityMatrix.FindRepositoryRoot();
        var known = RequiredRegions.Select(entry => entry.Region).ToHashSet(StringComparer.Ordinal);
        var stray = new List<string>();

        // A marker nobody writes into is worse than no marker: it looks generated and never is.
        foreach (var document in RequiredRegions.Select(entry => entry.Document).Distinct(StringComparer.Ordinal))
        {
            var text = File.ReadAllText(Path.Combine(root, document.Replace('/', Path.DirectorySeparatorChar)));
            stray.AddRange(Regex.Matches(text, "<!-- verification:([a-z-]+):(?:begin|end) -->")
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .Where(region => !known.Contains(region))
                .Select(region => $"{document}: '{region}' is marked up but never generated"));
        }

        Assert.AreEqual(0, stray.Count, string.Join(Environment.NewLine, stray));
    }

    [TestMethod]
    public void Documents_OpenAndCloseTheirRegionsInOrder()
    {
        var root = CompatibilityMatrix.FindRepositoryRoot();
        var problems = new List<string>();

        foreach (var document in RequiredRegions.Select(entry => entry.Document).Distinct(StringComparer.Ordinal))
        {
            var text = File.ReadAllText(Path.Combine(root, document.Replace('/', Path.DirectorySeparatorChar)));

            // Nesting or crossing markers would make the splice in build.ps1 swallow the wrong
            // span, so the markers have to read strictly begin, end, begin, end.
            var open = (string?)null;
            foreach (Match match in Regex.Matches(text, "<!-- verification:([a-z-]+):(begin|end) -->"))
            {
                var region = match.Groups[1].Value;
                var kind = match.Groups[2].Value;

                if (kind == "begin")
                {
                    if (open is not null)
                    {
                        problems.Add($"{document}: '{region}' opens while '{open}' is still open");
                    }

                    open = region;
                }
                else if (open != region)
                {
                    problems.Add($"{document}: '{region}' closes but '{open ?? "nothing"}' was open");
                    open = null;
                }
                else
                {
                    open = null;
                }
            }

            if (open is not null)
            {
                problems.Add($"{document}: '{open}' is never closed");
            }
        }

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
