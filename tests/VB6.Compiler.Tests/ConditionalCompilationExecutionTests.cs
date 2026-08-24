namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ConditionalCompilationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_SelectsElseIfBranchFromConditionalConstant()
    {
        var output = VB6TestProgram.RunLines("""
            #Const DEBUGMODE = 0
            #If DEBUGMODE Then
                Sub Main()
                    Debug.Print 1
                End Sub
            #ElseIf VBA7 Then
                Sub Main()
                    Debug.Print 7
                End Sub
            #Else
                Sub Main()
                    Debug.Print 9
                End Sub
            #End If
            """);

        CollectionAssert.AreEqual(new[] { "7" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_HandlesNestedConditionalBlocksAndExpressions()
    {
        var output = VB6TestProgram.RunLines("""
            #Const FEATURE = 1
            #If FEATURE = 1 Then
                #If Not VBA6 And VBA7 Then
                    Sub Main()
                        Debug.Print 42
                    End Sub
                #Else
                    Sub Main()
                        Debug.Print 1
                    End Sub
                #End If
            #Else
                Sub Main()
                    Debug.Print 0
                End Sub
            #End If
            """);

        CollectionAssert.AreEqual(new[] { "42" }, output);
    }

    [TestMethod]
    public void Analyze_ReportsMissingConditionalCompilationEnd()
    {
        var analysis = VBCompilation.Create("""
            #If VBA7 Then
                Sub Main()
                End Sub
            """, "Conditional.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6CC0006"));
    }
}
