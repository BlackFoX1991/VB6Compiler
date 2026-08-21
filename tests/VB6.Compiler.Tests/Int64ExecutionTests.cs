namespace VB6.Compiler.Tests;

[TestClass]
public sealed class Int64ExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesInt64ArithmeticAndForLoop()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Int64
                Dim i As LongLong
                value = 3000000000

                For i = 1 To 3
                    value = value + 1000000000
                Next i

                Debug.Print value
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("6000000000", standardOutput.Trim());
    }
}
