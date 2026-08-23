namespace VB6.Compiler.Tests;

[TestClass]
public sealed class UnsignedIntegerExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesUIntegerArithmeticAndForLoop()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As UInt32
                Dim i As UInteger
                value = CUInt(4000000000)
                value = value + 1

                For i = 1 To 2
                    value = value + 1
                Next i

                Debug.Print value
                Debug.Print CInt(value Mod 3)
            End Sub
            """, "Module1.bas");

        CollectionAssert.AreEqual(
            new[] { "4000000003", "1" },
            VB6TestProgram.SplitLines(VB6TestProgram.Run(compilation)));
    }
}
