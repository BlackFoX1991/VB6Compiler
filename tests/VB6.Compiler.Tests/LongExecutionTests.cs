namespace VB6.Compiler.Tests;

[TestClass]
public sealed class LongExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesLongArithmeticAndForLoop()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Long
                Dim i As Long
                value = 40000 + 20000

                For i = 1 To 3
                    value = value + 1
                Next i

                Debug.Print value
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("60003", standardOutput.Trim());
    }
}
