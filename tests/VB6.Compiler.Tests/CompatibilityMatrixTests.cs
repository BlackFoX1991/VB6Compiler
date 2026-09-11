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

    /// <summary>
    /// The successor of <c>Matrix_KeepsOracleVerificationEmptyUntilThereIsAnOracle</c>.
    ///
    /// That case held the axis at zero and said so in as many words: a future non-zero has to be
    /// argued for by deleting the assertion rather than by editing a number. The argument arrived
    /// on 2026-09-10 with a real VB6 SP6 and a harness that runs inside the suite, so the guard
    /// changes from "never" to "only with a case behind it": an expectation may claim
    /// <c>oracle-verified</c> only while it names a file in
    /// <c>tests/VB6.Compiler.Tests/Oracle*Tests.cs</c>, which is where a comparison against the
    /// original can live at all.
    ///
    /// That is the mechanisable half of the roadmap's rule. The other half -- that the case covers
    /// the expectation's **whole** surface and passes without a remainder -- stays a judgement, and
    /// a run with a known remainder keeps the expectation <c>documented-verified</c> with its rest
    /// as a card.
    /// </summary>
    [TestMethod]
    public void Matrix_BacksEveryOracleVerificationWithAnOracleCase()
    {
        var unbacked = CompatibilityMatrix.LoadExpectations()
            .Where(expectation => expectation.Verification == "oracle-verified")
            .Where(expectation => !expectation.TestRefs.Any(reference =>
                reference.StartsWith("tests/VB6.Compiler.Tests/Oracle", StringComparison.Ordinal)))
            .Select(expectation => expectation.Id)
            .ToArray();

        Assert.AreEqual(
            0,
            unbacked.Length,
            "oracle-verified ohne Orakelfall in den testRefs: " + string.Join(", ", unbacked));
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
        var oracleVerified = expectations.Count(expectation => expectation.Verification == "oracle-verified");
        var total = expectations.Count;

        Assert.AreEqual(total, implemented + partial + planned, "Statusachse implementation unvollständig.");
        Assert.AreEqual(
            total,
            documented + notYetVerified + oracleVerified,
            "Statusachse verification unvollständig -- oder es steht ein vierter Wert in der Datei.");

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
            $"{documented} `documented-verified`, {notYetVerified} `not-yet-verified`, {oracleVerified} `oracle-verified`",
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
