using System.Text.RegularExpressions;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DiagnosticCodeCoverageTests
{
    // These two guards are intentionally named here even though their production conditions are
    // difficult to reproduce portably (a directory/read-denied source and an incomplete project
    // reference metadata record). The inventory test still prevents either code from silently
    // disappearing from the documented diagnostic surface.
    private static readonly string[] GuardedCodes = ["VB6PRJ0002", "VB6PRJ0013"];

    [TestMethod]
    public void EveryProductionDiagnosticCodeIsCoveredByTestsOrAnExplicitGuard()
    {
        var root = FindRepositoryRoot();
        var productionCodes = ReadCodes(Path.Combine(root, "src"));
        var coveredCodes = ReadCodes(Path.Combine(root, "tests"));
        coveredCodes.UnionWith(GuardedCodes);

        CollectionAssert.AreEquivalent(
            Array.Empty<string>(),
            productionCodes.Except(coveredCodes).OrderBy(code => code).ToArray(),
            "A production diagnostic code has no explicit test/guard reference.");
    }

    private static HashSet<string> ReadCodes(string directory)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"VB6[A-Z]+[0-9]{4}"))
            {
                codes.Add(match.Value);
            }
        }

        return codes;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
