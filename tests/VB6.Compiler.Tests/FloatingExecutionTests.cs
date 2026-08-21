namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FloatingExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesSingleDivisionAndSingleLongPromotion()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim singleValue As Single
                Dim longValue As Long
                Dim doubleValue As Double

                singleValue = 1 / 2
                longValue = 40000
                doubleValue = singleValue + longValue

                If doubleValue = 40000.5 Then
                    Debug.Print 1
                Else
                    Debug.Print 0
                End If
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("1", standardOutput.Trim());
    }
}
