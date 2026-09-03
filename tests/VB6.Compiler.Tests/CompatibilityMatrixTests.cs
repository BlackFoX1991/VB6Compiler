using System.Text.Json;
using System.Text.RegularExpressions;

namespace VB6.Compiler.Tests;

/// <summary>
/// The closing gate of the compatibility matrix, mechanised.
///
/// A matrix is only worth something while its claims stay checkable. Three of them can be checked
/// by machine and are checked here: every expectation names tests, those tests exist, and the
/// counts quoted in the documentation are the counts in the file. The fourth is the rule that no
/// entry may claim <c>oracle-verified</c> without a run against a real VB6 SP6 -- no such run
/// exists, so the honest value is zero, and a future non-zero has to be argued for by deleting
/// this assertion rather than by editing a number.
/// </summary>
[TestClass]
public sealed class CompatibilityMatrixTests
{
    [TestMethod]
    public void Matrix_ReferencesTestsThatExist()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "docs", "vb6-sp6-compatibility-matrix.json")));

        var unresolved = new List<string>();
        foreach (var expectation in document.RootElement.GetProperty("expectations").EnumerateArray())
        {
            var id = expectation.GetProperty("id").GetString()!;
            var references = expectation.GetProperty("testRefs").EnumerateArray().ToArray();
            Assert.IsTrue(references.Length > 0, id + " names no test.");
            foreach (var reference in references)
            {
                if (!Resolves(root, reference.GetString()!))
                {
                    unresolved.Add(id + " -> " + reference.GetString());
                }
            }
        }

        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            var id = entry.GetProperty("id").GetString()!;
            foreach (var reference in entry.GetProperty("tests").EnumerateArray())
            {
                if (!Resolves(root, reference.GetString()!))
                {
                    unresolved.Add(id + " -> " + reference.GetString());
                }
            }
        }

        Assert.AreEqual(0, unresolved.Count, string.Join(Environment.NewLine, unresolved));
    }

    [TestMethod]
    public void Matrix_KeepsOracleVerificationEmptyUntilThereIsAnOracle()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "docs", "vb6-sp6-compatibility-matrix.json")));

        var claimed = document.RootElement.GetProperty("expectations").EnumerateArray()
            .Where(entry => entry.GetProperty("verification").GetString() == "oracle-verified")
            .Select(entry => entry.GetProperty("id").GetString())
            .ToArray();

        Assert.AreEqual(
            0,
            claimed.Length,
            "oracle-verified darf nur nach einem echten VB6-SP6-Lauf stehen: " + string.Join(", ", claimed));
    }

    [TestMethod]
    public void Matrix_CountsMatchTheDocumentedNumbers()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "docs", "vb6-sp6-compatibility-matrix.json")));

        var expectations = document.RootElement.GetProperty("expectations").EnumerateArray().ToArray();
        var implemented = expectations.Count(e => e.GetProperty("implementation").GetString() == "implemented");
        var partial = expectations.Count(e => e.GetProperty("implementation").GetString() == "partial");
        var planned = expectations.Count(e => e.GetProperty("implementation").GetString() == "planned");
        var documented = expectations.Count(e => e.GetProperty("verification").GetString() == "documented-verified");

        Assert.AreEqual(expectations.Length, implemented + partial + planned, "Statusachse unvollständig.");

        // Die Zahlen stehen an drei Stellen in der Dokumentation. Wandern sie auseinander, ist die
        // Matrix nicht mehr die Quelle -- und genau das soll auffallen.
        var roadmap = File.ReadAllText(Path.Combine(root, "docs", "ROADMAP.md"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        StringAssert.Contains(
            roadmap,
            $"**{expectations.Length} Erwartungen**, davon **{implemented} implemented**, **{partial} partial** und **{planned} planned**",
            "ROADMAP.md");
        StringAssert.Contains(roadmap, $"**{documented}/{expectations.Length} documented-verified**", "ROADMAP.md");
        StringAssert.Contains(
            readme,
            $"{expectations.Length} expectations ({implemented} implemented, {partial} partial, {planned} planned) with {documented}/{expectations.Length} documented-verified",
            "README.md");
    }

    private static bool Resolves(string root, string reference)
    {
        var normalized = reference.Replace('/', Path.DirectorySeparatorChar);
        var full = Path.Combine(root, normalized);
        if (File.Exists(full) || Directory.Exists(full))
        {
            return true;
        }

        // Ein Verweis darf ein Muster sein -- eine Familie von Tests statt einer Datei.
        var directory = Path.GetDirectoryName(full);
        var pattern = Path.GetFileName(full);
        if (directory is null || !Directory.Exists(directory) || !pattern.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        var expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        return Directory.EnumerateFiles(directory)
            .Any(file => Regex.IsMatch(Path.GetFileName(file), expression, RegexOptions.IgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "vb6-sp6-compatibility-matrix.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The compatibility matrix was not found above the test output.");
    }
}
