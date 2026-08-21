namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ChrIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesReachableAsciiChr()
    {
        const string source = """
            Sub Main()
                Debug.Print Chr(34)
                Debug.Print Chr(65)
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "\"", "A" }, VB6TestProgram.SplitLines(output), output);
    }

    /// <summary>
    /// A user-defined procedure of the same name shadows the intrinsic, exactly as in VB6. What
    /// the program prints is the only reliable evidence for which of the two was called.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_PrefersAUserFunctionOverTheIntrinsicChr()
    {
        var output = VB6TestProgram.Run("""
            Function Chr(ByVal value As Long) As String
                Chr = "custom"
            End Function

            Sub Main()
                Debug.Print Chr(34)
            End Sub
            """);

        Assert.AreEqual("custom", output.Trim());
    }

}
