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

    /// <summary>A user-defined Mid shadows the intrinsic, exactly as in VB6.</summary>
    [TestMethod]
    public void EmitManagedApplication_PrefersAUserFunctionOverTheIntrinsicMid()
    {
        var output = VB6TestProgram.Run("""
            Function Mid(ByVal value As String, ByVal start As Long, ByVal length As Long) As String
                Mid = "custom"
            End Function

            Sub Main()
                Debug.Print Mid("abc", 1, 1)
            End Sub
            """);

        Assert.AreEqual("custom", output.Trim());
    }

}
