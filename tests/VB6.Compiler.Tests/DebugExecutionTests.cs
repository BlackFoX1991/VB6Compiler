namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DebugExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ElidesDebugAssertFromCompiledProgram()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Assert False
                Debug.Print "after"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "after" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_DoesNotEvaluateElidedDebugAssertExpression()
    {
        var output = VB6TestProgram.RunLines("""
            Dim calls As Long

            Function Mark() As Boolean
                calls = calls + 1
                Mark = True
            End Function

            Sub Main()
                Debug.Assert Mark()
                Debug.Print calls
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0" }, output);
    }
}
