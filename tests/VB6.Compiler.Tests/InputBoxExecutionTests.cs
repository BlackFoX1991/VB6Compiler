namespace VB6.Compiler.Tests;

[TestClass]
public sealed class InputBoxExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesTheHeadlessDefaultResponse()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print InputBox("Name", "User", "fallback")
                Debug.Print "[" & InputBox("Empty") & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "fallback", "[]" }, VB6TestProgram.SplitLines(output), output);
    }
}
