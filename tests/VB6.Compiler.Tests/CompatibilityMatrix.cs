using System.Text.Json;

namespace VB6.Compiler.Tests;

/// <summary>
/// One expectation in <c>docs/vb6-sp6-compatibility-matrix.json</c>: a single named contract with
/// its two independent status axes. <see cref="Milestone"/> and <see cref="DependsOn"/> are set on
/// open cards only; a closed contract has no place in the remaining plan.
/// </summary>
internal sealed record MatrixExpectation(
    string Id,
    string MatrixEntry,
    string Implementation,
    string Verification,
    string? Milestone,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> TestRefs);

/// <summary>
/// One area of the matrix. Its two status values are not written by hand -- they are derived from
/// the expectations that name the area, and <see cref="Gap"/> says which cards keep it open.
/// </summary>
internal sealed record MatrixArea(
    string Id,
    string Implementation,
    string Verification,
    string? Gap,
    IReadOnlyList<string> Tests);

/// <summary>
/// Reads the compatibility matrix for the tests that check it.
///
/// The matrix is the single source for cards, status and dependencies, so the tests read the file
/// itself rather than a copy of its numbers. Everything here is deliberately dumb: it parses, it
/// does not judge. The rules live in the test methods, where a violation can name itself.
/// </summary>
internal static class CompatibilityMatrix
{
    public const string RelativePath = "docs/vb6-sp6-compatibility-matrix.json";

    /// <summary>Walks up from the test output until the matrix is found.</summary>
    public static string FindRepositoryRoot()
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

    public static JsonDocument Open() =>
        JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "docs", "vb6-sp6-compatibility-matrix.json")));

    public static IReadOnlyList<MatrixExpectation> LoadExpectations()
    {
        using var document = Open();
        return document.RootElement.GetProperty("expectations").EnumerateArray()
            .Select(expectation => new MatrixExpectation(
                expectation.GetProperty("id").GetString()!,
                expectation.GetProperty("matrixEntry").GetString()!,
                expectation.GetProperty("implementation").GetString()!,
                expectation.GetProperty("verification").GetString()!,
                OptionalString(expectation, "milestone"),
                StringArray(expectation, "dependsOn"),
                StringArray(expectation, "testRefs")))
            .ToArray();
    }

    public static IReadOnlyList<MatrixArea> LoadAreas()
    {
        using var document = Open();
        return document.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => new MatrixArea(
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("implementation").GetString()!,
                entry.GetProperty("verification").GetString()!,
                OptionalString(entry, "gap"),
                StringArray(entry, "tests")))
            .ToArray();
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> StringArray(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.GetString()!).ToArray()
            : Array.Empty<string>();
}
