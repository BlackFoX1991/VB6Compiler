namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ForEachSyntaxGuardTests
{
    [TestMethod]
    public void Analyze_ParsesForEachAndReportsExplicitSemanticGuard()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim item As Long
                Dim values(1 To 2) As Long
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "test.bas").Analyze();

        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsFalse(analysis.ParseResult.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6P0001"));
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0052"));
        Assert.IsFalse(analysis.Success);
    }

    [TestMethod]
    public void GenerateCSharp_DoesNotSilentlyDropForEachLoop()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim item As Long
                Dim values(1 To 2) As Long
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0052"));
    }
}
