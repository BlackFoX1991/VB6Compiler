namespace VB6.Compiler.Tests;

[TestClass]
public sealed class CompilationTests
{
    [TestMethod]
    public void Analyze_AcceptanceProgramPassesFrontEnd()
    {
        var compilation = VBCompilation.Create("""
            Option Explicit

            Sub Main()
                Dim x As Integer
                x = 10

                If x > 5 Then
                    Debug.Print x
                End If
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsTrue(analysis.Success);
        Assert.AreEqual(0, analysis.Diagnostics.Length);
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.AreEqual(1, analysis.SemanticModel!.Procedures.Length);
    }

    [TestMethod]
    public void Analyze_StopsBeforeBindingWhenParsingFails()
    {
        var compilation = VBCompilation.Create("Sub", "broken.bas");

        var analysis = compilation.Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6P")));
    }

    [TestMethod]
    public void Analyze_IncludesSemanticDiagnostics()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                missing = 10
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0001"));
    }

    [TestMethod]
    public void GenerateCSharp_ReturnsGeneratedSource()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 10
            End Sub
            """, "Module1.bas");

        var generation = compilation.GenerateCSharp();

        Assert.IsTrue(generation.Success);
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "public static void Main()");
        StringAssert.Contains(generation.Source, "VBConversions.CInt(10L)");
    }
}
