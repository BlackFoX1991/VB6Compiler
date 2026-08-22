namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FileStringIoExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsVariableLengthStrings()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim path As String
                Dim written As String
                Dim readBack As String

                path = "string.bin"
                written = "Grüße"
                Open path For Binary As #1
                Put #1, 1, written
                Close #1

                Open path For Binary As #1
                Get #1, 1, readBack
                Close #1
                Debug.Print readBack
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Grüße" }, VB6TestProgram.SplitLines(output), output);
    }
}
