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
