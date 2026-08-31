namespace VB6.Compiler.Tests;

/// <summary>
/// Debug.Print takes the same output list as Print #: any number of expressions joined by ';'
/// or ',', where a comma advances to the next 14-column print zone and a trailing separator
/// holds the line open. Only the single-expression form existed before, which made the most
/// ordinary multi-item Debug.Print in legacy code a parser error.
/// </summary>
[TestClass]
public sealed class DebugPrintOutputListExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_JoinsSemicolonSeparatedItemsOnOneLine()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print "a"; "b"; "c"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "abc" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_AdvancesToTheNextPrintZoneOnAComma()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print "a", "b"
            End Sub
            """);

        Assert.AreEqual("a", output[..1]);
        Assert.AreEqual('b', output[14]);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsTheLineOpenAfterATrailingSeparator()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print "a";
                Debug.Print "b"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "ab" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PrintsAnEmptyLineForABareDebugPrint()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print "x"
                Debug.Print
                Debug.Print "y"
            End Sub
            """);

        Assert.AreEqual(3, VB6TestProgram.SplitLines(output.Replace("\r\n", "\n").Replace("\n\n", "\n \n")).Length);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsTheSingleExpressionFormUnchanged()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print 42
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "42" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_MixesSeparatorsAndExpressions()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Long
                value = 7
                Debug.Print "v="; value; "!"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "v= 7!" }, output);
    }
}
