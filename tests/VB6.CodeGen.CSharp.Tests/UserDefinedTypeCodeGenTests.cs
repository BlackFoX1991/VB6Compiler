using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class UserDefinedTypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsUdtStructsParametersReturnsAndArrays()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Label As String
            End Type

            Public Current As Point

            Function Echo(ByRef value As Point) As Point
                Echo = value
            End Function

            Sub Main()
                Dim points(1 To 2) As Point
                Dim local As Point
                local = Echo(local)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private struct __vb6_udt_Point");
        StringAssert.Contains(source, "public int __vb6_member_X;");
        StringAssert.Contains(source, "public string __vb6_member_Label;");
        StringAssert.Contains(source, "private static __vb6_udt_Point __vb6_Current = default;");
        StringAssert.Contains(source, "private static __vb6_udt_Point __vb6_Echo(ref __vb6_udt_Point __vb6_arg_value)");
        StringAssert.Contains(source, "VBArray<__vb6_udt_Point> __vb6_points = new VBArray<__vb6_udt_Point>");
        StringAssert.Contains(source, "__vb6_local = __vb6_Echo(ref __vb6_local);");
    }

    [TestMethod]
    public void Emit_ProducesManagedAssemblyForUdtValueCopy()
    {
        var generation = VBCompilation.Create("""
            Type Point
                X As Long
            End Type

            Function Echo(ByVal value As Point) As Point
                Echo = value
            End Function

            Sub Main()
                Dim value As Point
                value = Echo(value)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        using var peStream = new MemoryStream();

        var emitResult = new CSharpAssemblyEmitter().Emit(generation.Source, "GeneratedUdtProgram", peStream);

        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
        Assert.IsTrue(peStream.Length > 0);
    }
}