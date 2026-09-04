namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ValWhitespaceExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ReadsANumberAcrossWhitespace()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Val("  1 2  ")
                Debug.Print Val("1 2 3")
                Debug.Print Val("24 and 57")
                Debug.Print Val("&HFF")
                Debug.Print Val("not a number")
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "12", "123", "24", "255", "0" }, output);
    }
}
