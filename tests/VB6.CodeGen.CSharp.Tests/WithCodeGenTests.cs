using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class WithCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsRefAliasForUdtVariable()
    {
        var generation = VBCompilation.Create("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim point As Point
                With point
                    .X = 41
                    Debug.Print .X
                End With
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success, FormatDiagnostics(generation));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "ref var __vb6_with_0 = ref __vb6_point;");
        StringAssert.Contains(generation.Source, "__vb6_with_0.__vb6_member_X = ");
        StringAssert.Contains(generation.Source, "VBDebug.Print(__vb6_with_0.__vb6_member_X);");
        AssertRoslynEmitSucceeds(generation.Source);
    }

    [TestMethod]
    public void Generate_EmitsNestedWithAliasesAgainstOuterMember()
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
                With outer
                    With .Child
                        .Value = 7
                        Debug.Print .Value
                    End With
                End With
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success, FormatDiagnostics(generation));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "ref var __vb6_with_0 = ref __vb6_outer;");
        StringAssert.Contains(
            generation.Source,
            "ref var __vb6_with_1 = ref __vb6_with_0.__vb6_member_Child;");
        StringAssert.Contains(generation.Source, "__vb6_with_1.__vb6_member_Value = ");
        AssertRoslynEmitSucceeds(generation.Source);
    }

    [TestMethod]
    public void Generate_EmitsRefAliasForArrayElementWithTarget()
    {
        var generation = VBCompilation.Create("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim points(1 To 2) As Point
                With points(1)
                    .X = 9
                    Debug.Print .X
                End With
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success, FormatDiagnostics(generation));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "ref var __vb6_with_0 = ref __vb6_points[");
        StringAssert.Contains(generation.Source, "__vb6_with_0.__vb6_member_X");
        AssertRoslynEmitSucceeds(generation.Source);
    }

    [TestMethod]
    public void Generate_EmitsByRefMemberFromWithAlias()
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
                With point
                    SetValue .X
                End With
                Debug.Print point.X
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(generation.Success, FormatDiagnostics(generation));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "__vb6_SetValue(ref __vb6_with_0.__vb6_member_X);");
        AssertRoslynEmitSucceeds(generation.Source);
    }

    private static void AssertRoslynEmitSucceeds(string source)
    {
        using var peStream = new MemoryStream();
        var result = new CSharpAssemblyEmitter().Emit(source, "GeneratedWithProgram", peStream);
        Assert.IsTrue(
            result.Success,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
        Assert.IsTrue(peStream.Length > 0);
    }

    private static string FormatDiagnostics(CSharpGenerationResult generation) =>
        string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}