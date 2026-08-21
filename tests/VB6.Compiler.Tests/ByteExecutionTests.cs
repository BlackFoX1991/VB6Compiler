namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ByteExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesByteConversionsAndArithmetic()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Byte
                Dim stepValue As Byte

                value = 200
                stepValue = 20
                value = value + stepValue
                value = value Mod 64

                Debug.Print value
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("28", standardOutput.Trim());
    }
}
