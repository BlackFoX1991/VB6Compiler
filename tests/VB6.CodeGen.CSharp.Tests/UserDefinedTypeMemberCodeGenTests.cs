using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class UserDefinedTypeMemberCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsMemberReadsWritesAndNestedChains()
    {
        var generation = VBCompilation.Create("""
            Type Inner
                Value As Long
            End Type

            Type Outer
                Child As Inner
            End Type

            Sub Main()
                Dim outer As Outer
                Dim result As Long
                outer.Child.Value = 42
                result = outer.Child.Value
                Debug.Print result
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success, FormatDiagnostics(generation));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(
            generation.Source,
            "__vb6_outer.__vb6_member_Child.__vb6_member_Value = ");
        StringAssert.Contains(
            generation.Source,
            "__vb6_result = __vb6_outer.__vb6_member_Child.__vb6_member_Value;");
        AssertRoslynEmitSucceeds(generation.Source);
    }

    [TestMethod]
    public void Generate_EmitsArrayElementMemberAssignment()
    {
        var generation = VBCompilation.Create("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim points(1 To 2) As Point
                points(1).X = 9
                Debug.Print points(1).X
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success, FormatDiagnostics(generation));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "__vb6_points[");
        StringAssert.Contains(generation.Source, "].__vb6_member_X");
        AssertRoslynEmitSucceeds(generation.Source);
    }

    [TestMethod]
    public void Generate_EmitsUdtMemberAsByRefArgument()
    {
        var generation = VBCompilation.Create("""
            Type Point
                X As Long
            End Type

            Sub SetValue(ByRef value As Long)
                value = 12
            End Sub

            Sub Main()
                Dim point As Point
                SetValue point.X
                Debug.Print point.X
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success, FormatDiagnostics(generation));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "__vb6_SetValue(ref __vb6_point.__vb6_member_X);");
        AssertRoslynEmitSucceeds(generation.Source);
    }

    private static void AssertRoslynEmitSucceeds(string source)
    {
        using var peStream = new MemoryStream();
        var result = new CSharpAssemblyEmitter().Emit(source, "GeneratedUdtMembers", peStream);
        Assert.IsTrue(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
        Assert.IsTrue(peStream.Length > 0);
    }

    private static string FormatDiagnostics(CSharpGenerationResult generation) =>
        string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}