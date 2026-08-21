using VB6.IR;

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
                Debug.Print Mid(s)
            End Sub
            """, "Module1.bas").Analyze();

        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0006");
        StringAssert.Contains(diagnostic.Message, "'Mid'");
        Assert.IsFalse(
            diagnostic.Message.Contains("__VB6", StringComparison.Ordinal),
            "A placeholder name must never reach the user.");
    }

    [TestMethod]
    public void Lower_CallsTheRuntimeDirectly()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim n As Integer
                n = CInt(42)
                Debug.Print CStr(n)
                Debug.Print Len("abc")
            End Sub
            """);

        // An intrinsic is a runtime operation, not a call into a generated procedure: nothing
        // named after the placeholder may survive lowering.
        CollectionAssert.IsSubsetOf(
            new[] { IrRuntimeMethod.CInt, IrRuntimeMethod.CStr, IrRuntimeMethod.StringLen },
            VB6TestIr.RuntimeCalls(program).ToArray());
        Assert.IsFalse(
            VB6TestIr.Procedures(program).Any(procedure =>
                procedure.Name.Contains("__VB6_INTRINSIC", StringComparison.Ordinal)),
            "No placeholder should survive into the lowered program.");
    }

    /// <summary>
    /// VB6 lets trailing intrinsic arguments be omitted. The backend emits exactly what the call
    /// site wrote and the runtime carries an overload per arity, so no filler argument is invented.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_AcceptsAnOmittedTrailingIntrinsicArgument()
    {
        var lines = VB6TestProgram.RunLines("""
            Sub Main()
                Dim s As String
                s = "abcdef"
                Debug.Print Mid(s, 2)
                Debug.Print Mid(s, 2, 3)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "bcdef", "bcd" }, lines);
    }

    [TestMethod]
    public void Analyze_ReportsTheAcceptedRangeWhenTheArityIsWrong()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim s As String
                Debug.Print Mid(s)
            End Sub
            """, "Module1.bas").Analyze();

        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0006");
        StringAssert.Contains(diagnostic.Message, "2 to 3");
    }

    /// <summary>A user procedure of the same name wins, as it does in VB6.</summary>
    [TestMethod]
    public void EmitManagedApplication_LetsUserDeclarationsShadowIntrinsics()
    {
        var output = VB6TestProgram.Run("""
            Function CInt(ByVal Value As Long) As Long
                CInt = Value + 1
            End Function

            Sub Main()
                Debug.Print CInt(1)
            End Sub
            """);

        Assert.AreEqual("2", output.Trim());
    }
}
