using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class UserDefinedTypeMemberArrayAssignmentCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsDirectWithAndNestedUdtArrayMemberWrites()
    {
        var analysis = VBCompilation.Create("""
            Type Child
                Value As Long
            End Type

            Type Record
                Values(1 To 3) As Long
                Children(1 To 2) As Child
            End Type

            Sub Main()
                Dim record As Record
                record.Values(2) = 9
                record.Children(1).Value = 3
                With record
                    .Values(1) = 5
                End With
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));

        var source = new CSharpGenerator().Generate(analysis.SemanticModel);

        StringAssert.Contains(source, "__vb6_record.__vb6_member_Values[");
        StringAssert.Contains(source, "] = VBConversions.CLng(");
        StringAssert.Contains(source, "__vb6_record.__vb6_member_Children[");
        StringAssert.Contains(source, "].__vb6_member_Value = VBConversions.CLng(");
        StringAssert.Contains(source, "__vb6_with_0.__vb6_member_Values[");

        using var peStream = new MemoryStream();
        var emitResult = new CSharpAssemblyEmitter().Emit(source, "GeneratedUdtMemberArrayWriteProgram", peStream);
        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
    }
}
