namespace VB6.Compiler.Tests;

[TestClass]
public sealed class TypeOfGuardTests
{
    /// <summary>
    /// Parsing TypeOf must not make it look supported. Without the guard the expression would bind
    /// to an error node and the condition could be lowered as something else entirely.
    /// </summary>
    [TestMethod]
    public void Analyze_ReportsTypeOfUntilTheObjectModelExists()
    {
        var analysis = VBCompilation.Create("""
            Sub Apply(ctlControl As Long)
                If TypeOf ctlControl Is CheckBox Then Debug.Print 1
            End Sub

            Sub Main()
                Apply 1
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0060");
        StringAssert.Contains(diagnostic.Message, "CheckBox");
    }
}
