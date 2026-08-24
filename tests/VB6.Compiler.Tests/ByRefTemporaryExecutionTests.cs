using VB6.Semantics;

namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 accepts a literal, an expression, or a function result where a ByRef parameter is declared.
/// It passes a temporary and discards the write-back. This was the single largest semantic blocker
/// in the conformance corpus at 409 occurrences.
/// </summary>
[TestClass]
public sealed class ByRefTemporaryExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_PassesNonVariableArgumentsByRefThroughTemporaries()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Private Function Twice(ByVal N As Long) As Long
                Twice = N * 2
            End Function

            Public Sub Main()
                Dim keep As Long
                keep = 10

                Bump keep
                Debug.Print keep

                Bump 5
                Bump Twice(3)
                Bump keep + 1
                Debug.Print keep
            End Sub
            """,
            "11",
            "11");
    }

    /// <summary>
    /// Parentheses force an argument to be evaluated to a value, so the callee cannot write back.
    /// Only a Call statement has a parenthesized argument list, which is what separates
    /// <c>Call Bump(keep)</c> from <c>Bump (keep)</c> and <c>Call Bump((keep))</c>.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_TreatsParenthesizedArgumentsAsByValue()
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
                Call Bump(keep)
                Debug.Print keep

                keep = 0
                Bump (keep)
                Debug.Print keep

                keep = 0
                Call Bump((keep))
                Debug.Print keep
            End Sub
            """,
            "1",
            "0",
            "0");
    }


    [TestMethod]
    public void Analyze_KeepsByRefTypeMismatchAnError()
    {
        var analysis = VBCompilation.Create("""
            Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim small As Integer
                Bump small
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success, "VB6 reports a ByRef argument type mismatch for a variable of the wrong type.");
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0008"));
    }

    [TestMethod]
    public void Analyze_AllowsTypedVariablesForByRefVariantParameters()
    {
        var analysis = VBCompilation.Create("""
            Sub Append(ByRef value As Variant)
                value = value & "!"
            End Sub

            Sub Main()
                Dim text As String
                text = "legacy"
                Append text
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
        var main = analysis.SemanticModel!.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Last();
        Assert.IsTrue(invocation.Arguments.Single().RequiresByRefTemporary);
        Assert.IsInstanceOfType<BoundConversionExpression>(invocation.Arguments.Single().Expression);
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
