namespace VB6.Compiler.Tests;

[TestClass]
public sealed class MemberArrayForEachExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesForEachOverFixedPrimitiveUdtArrayMember()
    {
        const string source = """
            Type Holder
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim item
                Dim holder As Holder
                holder.Values(1) = 7
                holder.Values(2) = 8
                holder.Values(3) = 9

                For Each item In holder.Values
                    Debug.Print item
                Next item
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "7", "8", "9" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesForEachOverImplicitWithArrayMemberAndExitFor()
    {
        const string source = """
            Type Holder
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim item
                Dim holder As Holder
                holder.Values(1) = 11
                holder.Values(2) = 22
                holder.Values(3) = 33

                With holder
                    For Each item In .Values
                        Debug.Print item
                        Exit For
                    Next item
                End With
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "11" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void Analyze_ForEachOverScalarUdtMemberRemainsGuarded()
    {
        var analysis = VBCompilation.Create("""
            Type Holder
                Value As Long
            End Type

            Sub Main()
                Dim item
                Dim holder As Holder
                For Each item In holder.Value
                    Debug.Print item
                Next item
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.Contains(
            analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            "VB6S0055");
    }

}
