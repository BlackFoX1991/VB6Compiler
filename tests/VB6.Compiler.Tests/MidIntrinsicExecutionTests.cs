namespace VB6.Compiler.Tests;

[TestClass]
public sealed class MidIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesThreeArgumentMidAndMidDollar()
    {
        const string source = """
            Sub Main()
                Debug.Print Mid("abcdef", 2, 3)
                Debug.Print Mid$("abcdef", 5, 20)
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "bcd", "ef" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void GenerateCSharp_RewritesBuiltInMidButPreservesUserFunction()
    {
        var builtIn = VBCompilation.Create("""
            Sub Main()
                Debug.Print Mid("abc", 2, 1)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(builtIn.Success, string.Join(Environment.NewLine, builtIn.Diagnostics));
        Assert.IsNotNull(builtIn.Source);
        StringAssert.Contains(builtIn.Source, "VBStrings.Mid(\"abc\"");

        var userDefined = VBCompilation.Create("""
            Function Mid(ByVal value As String, ByVal start As Long, ByVal length As Long) As String
                Mid = "custom"
            End Function

            Sub Main()
                Debug.Print Mid("abc", 1, 1)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(userDefined.Success, string.Join(Environment.NewLine, userDefined.Diagnostics));
        Assert.IsNotNull(userDefined.Source);
        StringAssert.Contains(userDefined.Source, "__vb6_Mid(");
        Assert.IsFalse(userDefined.Source.Contains("VBStrings.Mid(", StringComparison.Ordinal));
    }

}
