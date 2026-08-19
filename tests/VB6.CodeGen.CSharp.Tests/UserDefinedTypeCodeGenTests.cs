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
    public void Generate_EmitsFixedUdtArrayStorageBoundsAndCloneHelpers()
    {
        var analysis = VBCompilation.Create("""
            Option Base 1

            Type Child
                Values(2 To 4) As Long
            End Type

            Type Parent
                Child As Child
                Flags(3) As Integer
            End Type

            Sub Main()
                Dim value As Parent
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));

        var source = new CSharpGenerator().Generate(analysis.SemanticModel);

        StringAssert.Contains(source, "private VBArray<int>? __vb6_array_Values;");
        StringAssert.Contains(source, "public VBArray<int> __vb6_member_Values =>");
        StringAssert.Contains(source, "__vb6_array_Values ??= new VBArray<int>(new VBArrayBound(2, 4));");
        StringAssert.Contains(source, "private VBArray<short>? __vb6_array_Flags;");
        StringAssert.Contains(source, "__vb6_array_Flags ??= new VBArray<short>(new VBArrayBound(1, 3));");
        StringAssert.Contains(source, "public __vb6_udt_Child __vb6_clone()");
        StringAssert.Contains(source, "__vb6_copy.__vb6_array_Values = __vb6_array_Values.Clone();");
        StringAssert.Contains(source, "public __vb6_udt_Parent __vb6_clone()");
        StringAssert.Contains(source, "__vb6_copy.__vb6_member_Child = __vb6_member_Child.__vb6_clone();");
        StringAssert.Contains(source, "__vb6_copy.__vb6_array_Flags = __vb6_array_Flags.Clone();");

        using var peStream = new MemoryStream();
        var emitResult = new CSharpAssemblyEmitter().Emit(source, "GeneratedUdtArrayStorageProgram", peStream);

        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
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
