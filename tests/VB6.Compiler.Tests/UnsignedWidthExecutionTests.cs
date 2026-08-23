namespace VB6.Compiler.Tests;

[TestClass]
public sealed class UnsignedWidthExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesUShortAndULongArithmetic()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim small As UInt16
                Dim wide As ULong
                Dim i As UShort

                small = CUShort(65534)
                small = small + 1
                wide = CULng("18446744073709551614")
                wide = wide + 1

                For i = 1 To 2
                    small = small - 1
                Next i

                Debug.Print small
                Debug.Print wide
            End Sub
            """, "Module1.bas");

        CollectionAssert.AreEqual(
            new[] { "65533", "18446744073709551615" },
            VB6TestProgram.SplitLines(VB6TestProgram.Run(compilation)));
    }
}
