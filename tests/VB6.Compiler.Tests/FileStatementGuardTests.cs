namespace VB6.Compiler.Tests;

/// <summary>
/// The binary file statements parse but have no runtime yet. Binding returns null for statements
/// it does not understand, which would drop them from the generated program silently - a wrong
/// program rather than a reported gap. VB6S0057 keeps that from happening.
/// </summary>
[TestClass]
public sealed class FileStatementGuardTests
{
    [TestMethod]
    public void Analyze_ReportsEveryUnimplementedFileStatement()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim buffer As Long
                Open "data.bin" For Binary As #1
                Put #1, 1, buffer
                Seek #1, 1
                Get #1, 1, buffer
                Close #1
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        var reported = analysis.Diagnostics
            .Where(diagnostic => diagnostic.Code == "VB6S0057")
            .Select(diagnostic => diagnostic.Message)
            .ToArray();

        Assert.AreEqual(5, reported.Length, string.Join(Environment.NewLine, reported));
        foreach (var keyword in new[] { "Open", "Put", "Seek", "Get", "Close" })
        {
            Assert.IsTrue(
                reported.Any(message => message.Contains($"'{keyword}'", StringComparison.Ordinal)),
                $"Expected a report naming {keyword}.");
        }
    }

    [TestMethod]
    public void GenerateCSharp_StopsRatherThanEmittingAProgramWithoutTheFileStatements()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Close #1
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
    }
}
