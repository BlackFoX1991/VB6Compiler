using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class UserDefinedTypeMemberArrayCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsUdtArrayMemberReadsWithAndByRefArguments()
    {
        var analysis = VBCompilation.Create("""
            Type Record
                Values(1 To 3) As Long
            End Type

            Sub SetValue(ByRef value As Long)
                value = 10
            End Sub

            Sub Main()
                Dim record As Record
                Debug.Print record.Values(2)
                SetValue record.Values(3)
                With record
                    Debug.Print .Values(1)
                End With
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));

        var source = new CSharpGenerator().Generate(analysis.SemanticModel);

        StringAssert.Contains(source, "__vb6_record.__vb6_member_Values[");
        StringAssert.Contains(source, "ref __vb6_record.__vb6_member_Values[");
        StringAssert.Contains(source, "__vb6_with_0.__vb6_member_Values[");

        using var peStream = new MemoryStream();
        var emitResult = new CSharpAssemblyEmitter().Emit(source, "GeneratedUdtMemberArrayProgram", peStream);
        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
    }
}
