using System.Text.RegularExpressions;

namespace VB6.Compiler.Tests;

/// <summary>
/// The closing gate of the compatibility matrix, mechanised.
///
/// A matrix is only worth something while its claims stay checkable. Two of them are checked here:
/// every expectation names tests and those tests exist, and the counts quoted in the documentation
/// are the counts in the file. The third is the rule that no entry may claim
/// <c>oracle-verified</c> without a run against a real VB6 SP6 -- no such run exists, so the honest
/// value is zero, and a future non-zero has to be argued for by deleting this assertion rather than
/// by editing a number.
///
/// The status, dependency and milestone rules are checked in
/// <see cref="CompatibilityMatrixStatusTests"/>.
/// </summary>
[TestClass]
public sealed class CompatibilityMatrixTests
{
    [TestMethod]
    public void Matrix_ReferencesTestsThatExist()
    {
        var root = CompatibilityMatrix.FindRepositoryRoot();
        var unresolved = new List<string>();

        foreach (var expectation in CompatibilityMatrix.LoadExpectations())
        {
            Assert.IsTrue(expectation.TestRefs.Count > 0, expectation.Id + " names no test.");
            unresolved.AddRange(expectation.TestRefs
                .Where(reference => !Resolves(root, reference))
                .Select(reference => expectation.Id + " -> " + reference));
        }

        foreach (var area in CompatibilityMatrix.LoadAreas())
        {
            unresolved.AddRange(area.Tests
                .Where(reference => !Resolves(root, reference))
                .Select(reference => area.Id + " -> " + reference));
        }

        Assert.AreEqual(0, unresolved.Count, string.Join(Environment.NewLine, unresolved));
    }

    [TestMethod]
    public void Matrix_KeepsOracleVerificationEmptyUntilThereIsAnOracle()
    {
        var claimed = CompatibilityMatrix.LoadExpectations()
            .Where(expectation => expectation.Verification == "oracle-verified")
            .Select(expectation => expectation.Id)
            .ToArray();

        Assert.AreEqual(
            0,
            claimed.Length,
            "oracle-verified darf nur nach einem echten VB6-SP6-Lauf stehen: " + string.Join(", ", claimed));
    }

    [TestMethod]
    public void Matrix_CountsMatchTheDocumentedNumbers()
    {
        var root = CompatibilityMatrix.FindRepositoryRoot();
        var expectations = CompatibilityMatrix.LoadExpectations();

        var implemented = expectations.Count(expectation => expectation.Implementation == "implemented");
        var partial = expectations.Count(expectation => expectation.Implementation == "partial");
        var planned = expectations.Count(expectation => expectation.Implementation == "planned");
        var documented = expectations.Count(expectation => expectation.Verification == "documented-verified");
        var notYetVerified = expectations.Count(expectation => expectation.Verification == "not-yet-verified");
        var total = expectations.Count;

        Assert.AreEqual(total, implemented + partial + planned, "Statusachse implementation unvollständig.");
        Assert.AreEqual(
            total,
            documented + notYetVerified,
            "Statusachse verification unvollständig -- oder es steht ein dritter Wert in der Datei.");

        // Die Zahlen stehen an vier Stellen in der Dokumentation. Wandern sie auseinander, ist die
        // Matrix nicht mehr die Quelle -- und genau das soll auffallen.
        var roadmap = File.ReadAllText(Path.Combine(root, "docs", "ROADMAP.md"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var instructions = File.ReadAllText(Path.Combine(root, "CLAUDE.md"));

        StringAssert.Contains(
            roadmap,
            $"**{total} Erwartungen**, davon **{implemented} implemented**, **{partial} partial** und **{planned} planned**",
            "ROADMAP.md");
        StringAssert.Contains(roadmap, $"**{documented}/{total} documented-verified**", "ROADMAP.md");
        StringAssert.Contains(
            readme,
            $"{total} expectations ({implemented} implemented, {partial} partial, {planned} planned) with {documented}/{total} documented-verified",
            "README.md");
        StringAssert.Contains(
            instructions,
            $"Die Matrix enthält {total} Erwartungen: {implemented} `implemented`, {partial} `partial`, {planned} `planned`",
            "CLAUDE.md");
        StringAssert.Contains(
            instructions,
            $"{documented} `documented-verified`, {notYetVerified} `not-yet-verified`, 0 `oracle-verified`",
            "CLAUDE.md");
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
}
