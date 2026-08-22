namespace VB6.Compiler.Tests;

[TestClass]
public sealed class MsgBoxExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesTheHeadlessButtonDefaults()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print MsgBox("Ready")
                Debug.Print MsgBox("Continue", 4)
                Debug.Print MsgBox("Retry", 5)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1", "6", "4" }, VB6TestProgram.SplitLines(output), output);
    }
}
