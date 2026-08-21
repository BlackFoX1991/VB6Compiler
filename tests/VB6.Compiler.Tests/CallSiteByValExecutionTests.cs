namespace VB6.Compiler.Tests;

/// <summary>
/// A call site may override how an argument is passed. VB6 code does this against Declare
/// parameters typed As Any, as in <c>CopyMemory dst, ByVal VarPtr(src), 4</c>, which is listed in
/// the roadmap blocker table and appears 13 times in one conformance module.
/// </summary>
[TestClass]
public sealed class CallSiteByValExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ByValAtTheCallSiteOverridesAByRefParameter()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Public Sub Main()
                Dim keep As Long

                keep = 0
                Bump keep
                Debug.Print keep

                keep = 0
                Bump ByVal keep
                Debug.Print keep
            End Sub
            """,
            "1",
            "0");
    }

    [TestMethod]
    public void EmitManagedApplication_ByRefAtTheCallSiteKeepsTheReference()
    {
        Run("""
            Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim keep As Long
                keep = 0
                Bump ByRef keep
                Debug.Print keep
            End Sub
            """,
            "1");
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
