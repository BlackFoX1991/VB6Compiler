using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class UserDefinedTypeValueCopyCodeGenTests
{
    [TestMethod]
    public void Generate_ClonesManagedUdtStorageAtValueCopyBoundaries()
    {
        var analysis = VBCompilation.Create("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Type Holder
                Child As Record
            End Type

            Sub Consume(ByVal value As Record)
            End Sub

            Sub Touch(ByRef value As Record)
            End Sub

            Function Copy(ByVal value As Record) As Record
                Copy = value
            End Function

            Sub Main()
                Dim value As Record
                Dim copied As Record
                Dim items(1 To 1) As Record
                Dim holder As Holder

                copied = value
                items(1) = value
                holder.Child = value
                Consume value
                Touch value
                copied = Copy(value)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));

        var source = new CSharpGenerator().Generate(analysis.SemanticModel);

        StringAssert.Contains(source, "__vb6_copied = __vb6_value.__vb6_clone();");
        StringAssert.Contains(source, "__vb6_items[");
        StringAssert.Contains(source, "] = __vb6_value.__vb6_clone();");
        StringAssert.Contains(source, "__vb6_holder.__vb6_member_Child = __vb6_value.__vb6_clone();");
        StringAssert.Contains(source, "__vb6_Consume(__vb6_value.__vb6_clone());");
        StringAssert.Contains(source, "__vb6_Touch(ref __vb6_value);");
        StringAssert.Contains(source, "__vb6_return = __vb6_arg_value.__vb6_clone();");
        StringAssert.Contains(source, "__vb6_Copy(__vb6_value.__vb6_clone()).__vb6_clone()");

        using var peStream = new MemoryStream();
        var emitResult = new CSharpAssemblyEmitter().Emit(source, "GeneratedManagedUdtCopyProgram", peStream);
        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
    }

    [TestMethod]
    public void Generate_KeepsPlainUdtCopiesAsStructCopies()
    {
        var generation = VBCompilation.Create("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim source As Point
                Dim copied As Point
                copied = source
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "__vb6_copied = __vb6_source;");
        Assert.IsFalse(generation.Source.Contains("__vb6_clone()", StringComparison.Ordinal));
    }
}
