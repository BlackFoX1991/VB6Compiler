namespace VB6.Compiler.Tests;

/// <summary>
/// Intrinsics travel the normal call path and carry the runtime method the backend calls. Before
/// that, they were bound under placeholder names and the generated C# was rewritten with a string
/// replace, which broke the layer boundary and leaked the placeholders into diagnostics.
/// </summary>
[TestClass]
public sealed class IntrinsicSymbolTests
{
    [TestMethod]
    public void Analyze_NamesTheIntrinsicInDiagnostics()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim s As String
                Debug.Print Mid(s, 3)
            End Sub
            """, "Module1.bas").Analyze();

        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0006");
        StringAssert.Contains(diagnostic.Message, "'Mid'");
        Assert.IsFalse(
            diagnostic.Message.Contains("__VB6", StringComparison.Ordinal),
            "A placeholder name must never reach the user.");
    }

    [TestMethod]
    public void GenerateCSharp_CallsTheRuntimeDirectly()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim n As Integer
                n = CInt(42)
                Debug.Print CStr(n)
                Debug.Print Len("abc")
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        StringAssert.Contains(generation.Source, "VBConversions.CInt(");
        StringAssert.Contains(generation.Source, "VBConversions.CStr(");
        StringAssert.Contains(generation.Source, "VBStrings.Len(");
        Assert.IsFalse(
            generation.Source!.Contains("__VB6_INTRINSIC", StringComparison.Ordinal),
            "No placeholder should survive into the generated source.");
    }

    /// <summary>A user procedure of the same name wins, as it does in VB6.</summary>
    [TestMethod]
    public void GenerateCSharp_LetsUserDeclarationsShadowIntrinsics()
    {
        var generation = VBCompilation.Create("""
            Function CInt(ByVal Value As Long) As Long
                CInt = Value + 1
            End Function

            Sub Main()
                Debug.Print CInt(1)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success);
        StringAssert.Contains(generation.Source, "__vb6_CInt(");
    }
}
