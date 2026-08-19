using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class CSharpGeneratorTests
{
    [TestMethod]
    public void Generate_EmitsAcceptanceProgram()
    {
        var analysis = VBCompilation.Create("""
            Option Explicit

            Sub Main()
                Dim x As Integer
                x = 10

                If x > 5 Then
                    Debug.Print x
                End If
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "public static void Main()");
        StringAssert.Contains(source, "short __vb6_x = 0;");
        StringAssert.Contains(source, "__vb6_x = VBConversions.CInt(10L);");
        StringAssert.Contains(source, "if (VBOperators.Greater(__vb6_x, VBConversions.CInt(5L)))");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_x);");
    }

    [TestMethod]
    public void Generate_EmitsVbConversionCalls()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = "10"
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_x = VBConversions.CInt(\"10\");");
    }

    [TestMethod]
    public void Generate_EmitsProcedureCalls()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Helper
            End Sub

            Sub Helper()
                Debug.Print 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_Helper();");
        StringAssert.Contains(source, "private static void __vb6_Helper()");
    }

    [TestMethod]
    public void Generate_EmitsByRefAndByValParameters()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 5
                Call Update(x)
                Call Observe(x)
            End Sub

            Sub Update(value As Integer)
                value = 10
            End Sub

            Sub Observe(ByVal value As Integer)
                value = 20
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private static void __vb6_Update(ref short __vb6_arg_value)");
        StringAssert.Contains(source, "private static void __vb6_Observe(short __vb6_arg_value)");
        StringAssert.Contains(source, "__vb6_Update(ref __vb6_x);");
        StringAssert.Contains(source, "__vb6_Observe(__vb6_x);");
        StringAssert.Contains(source, "__vb6_arg_value = VBConversions.CInt(10L);");
    }

    [TestMethod]
    public void Generate_EmitsFunctionReturnSlotAndCallExpression()
    {
        var analysis = VBCompilation.Create("""
            Function Add(ByVal left As Integer, ByVal right As Integer) As Integer
                Add = left + right
            End Function

            Sub Main()
                Dim result As Integer
                result = Add(5, 7)
                Debug.Print result
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private static short __vb6_Add(short __vb6_arg_left, short __vb6_arg_right)");
        StringAssert.Contains(source, "short __vb6_return = 0;");
        StringAssert.Contains(source, "__vb6_return = VBOperators.AddInteger(__vb6_arg_left, __vb6_arg_right);");
        StringAssert.Contains(source, "return __vb6_return;");
        StringAssert.Contains(source, "__vb6_result = __vb6_Add(VBConversions.CInt(5L), VBConversions.CInt(7L));");
    }

    [TestMethod]
    public void Generate_EmitsForWhileDoAndExitTargets()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim i As Integer
                i = 0

                For i = 3 To 1 Step -1
                    Do
                        Exit For
                    Loop
                Next i

                While i < 2
                    i = i + 1
                Wend

                Do
                    i = i + 1
                Loop Until i = 3
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "short __vb6_for_limit_");
        StringAssert.Contains(source, "short __vb6_for_step_");
        StringAssert.Contains(source, "VBOperators.LessOrEqual(__vb6_i");
        StringAssert.Contains(source, "VBOperators.GreaterOrEqual(__vb6_i");
        StringAssert.Contains(source, "while (VBOperators.Less(__vb6_i, VBConversions.CInt(2L)))");
        StringAssert.Contains(source, "do");
        StringAssert.Contains(source, "while (!(VBOperators.Equal(__vb6_i, VBConversions.CInt(3L))));");
        StringAssert.Contains(source, "goto __vb6_loop_exit_");
    }

    [TestMethod]
    public void Emit_ProducesManagedAssembly()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 10
                Debug.Print x
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);
        using var peStream = new MemoryStream();

        var emitResult = new CSharpAssemblyEmitter().Emit(source, "GeneratedProgram", peStream);

        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
        Assert.IsTrue(peStream.Length > 0);
    }
}
