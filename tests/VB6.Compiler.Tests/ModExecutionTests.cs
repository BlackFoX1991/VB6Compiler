namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ModExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesIntegerModExpression()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Integer
                value = 17 Mod 5
                Debug.Print value
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("2", standardOutput.Trim());
    }
}
