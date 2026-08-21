namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DynamicForEachExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesDynamicArrayForEachInRuntimeOrder()
    {
        const string source = """
            Sub Main()
                Dim item As Variant
                Dim values() As Long
                ReDim values(2 To 4)
                values(2) = 20
                values(3) = 30
                values(4) = 40

                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "20", "30", "40" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesForEachOverArrayParameterAndExitFor()
    {
        const string source = """
            Sub PrintFirst(values() As Long)
                Dim item As Variant
                For Each item In values
                    Debug.Print item
                    Exit For
                Next item
            End Sub

            Sub Main()
                Dim values() As Long
                ReDim values(5 To 7)
                values(5) = 50
                values(6) = 60
                values(7) = 70
                Call PrintFirst(values)
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "50" },
            VB6TestProgram.SplitLines(output),
            output);
    }

}
