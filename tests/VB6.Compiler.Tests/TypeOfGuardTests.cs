namespace VB6.Compiler.Tests;

[TestClass]
public sealed class TypeOfGuardTests
{
    /// <summary>
    /// TypeOf now binds for project class symbols; an external control type still needs a declared
    /// reference/type definition before it can be resolved.
    /// </summary>
    [TestMethod]
    public void Analyze_ReportsUnknownTypeOfTarget()
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
        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0003");
        StringAssert.Contains(diagnostic.Message, "CheckBox");
    }
}
