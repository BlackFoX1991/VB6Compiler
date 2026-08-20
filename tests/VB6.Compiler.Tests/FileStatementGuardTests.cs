namespace VB6.Compiler.Tests;

/// <summary>
/// Binding returns null for statements it does not understand, so a file statement that is neither
/// lowered nor reported would vanish from the generated program without a word - a wrong program
/// rather than a reported gap. These tests pin both directions: what is supported reaches the
/// output, and what is not is named.
/// </summary>
[TestClass]
public sealed class FileStatementGuardTests
{
    [TestMethod]
    public void GenerateCSharp_EmitsEverySupportedFileStatement()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim buffer As Long
                Open "data.bin" For Binary As #1
                Put #1, 1, buffer
                Seek #1, 1
                Get #1, 1, buffer
                Close #1
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));

        foreach (var expected in new[]
                 {
                     "VBFiles.OpenBinary(",
                     "VBFiles.Put(",
                     "VBFiles.Seek(",
                     "VBFiles.GetLong(",
                     "VBFiles.Close("
                 })
        {
            StringAssert.Contains(generation.Source, expected);
        }
    }

    [TestMethod]
    public void GenerateCSharp_ClosesEveryFileForABareClose()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Close
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success);
        StringAssert.Contains(generation.Source, "VBFiles.CloseAll();");
    }

    [TestMethod]
    public void Analyze_StopsRatherThanEmittingAProgramMissingAnUnsupportedTransfer()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim text As String
                Open "a.bin" For Binary As #1
                Put #1, 1, text
                Close #1
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0058"));
    }
}
