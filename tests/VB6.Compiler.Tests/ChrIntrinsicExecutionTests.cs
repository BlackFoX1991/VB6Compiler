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

    [TestMethod]
    public void GenerateCSharp_RewritesBuiltInChrButPreservesUserFunction()
    {
        var builtIn = VBCompilation.Create("""
            Sub Main()
                Debug.Print Chr(34)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(builtIn.Success, string.Join(Environment.NewLine, builtIn.Diagnostics));
        Assert.IsNotNull(builtIn.Source);
        StringAssert.Contains(builtIn.Source, "VBStrings.Chr(");
        Assert.IsFalse(builtIn.Diagnostics.Any(diagnostic =>
            diagnostic.Code == "VB6S0005" && diagnostic.Message.Contains("Chr", StringComparison.OrdinalIgnoreCase)));

        var userDefined = VBCompilation.Create("""
            Function Chr(ByVal value As Long) As String
                Chr = "custom"
            End Function

            Sub Main()
                Debug.Print Chr(34)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(userDefined.Success, string.Join(Environment.NewLine, userDefined.Diagnostics));
        Assert.IsNotNull(userDefined.Source);
        StringAssert.Contains(userDefined.Source, "__vb6_Chr(");
        Assert.IsFalse(userDefined.Source.Contains("VBStrings.Chr(", StringComparison.Ordinal));
    }

}
