namespace VB6.Compiler.Tests;

/// <summary>
/// A VB6 Function without an As clause returns Variant. The shape comes straight from the
/// conformance corpus, where <c>Function SetImportUsed(Name As String, Offset As Long)</c> was the
/// first error in two modules.
/// </summary>
[TestClass]
public sealed class UntypedFunctionExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesUntypedFunctionReturningVariant()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Function Doubled(ByVal Value As Long)
                Doubled = Value * 2
            End Function

            Public Sub Main()
                Debug.Print Doubled(21)
            End Sub
            """,
            "42");
    }

    [TestMethod]
    public void Analyze_BindsUntypedFunctionReturnTypeAsVariant()
    {
        var analysis = VBCompilation.Create("""
            Function Untyped(ByVal Value As Long)
                Untyped = Value
            End Function

            Sub Main()
                Debug.Print Untyped(1)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));

        var function = analysis.SemanticModel!.Procedures.Single(procedure =>
            string.Equals(procedure.Symbol.Name, "Untyped", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("Variant", function.Symbol.ReturnType!.Name);
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            expectedLines,
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
