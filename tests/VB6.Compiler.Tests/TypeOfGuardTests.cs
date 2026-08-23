namespace VB6.Compiler.Tests;

[TestClass]
public sealed class TypeOfGuardTests
{
    [TestMethod]
    public void Analyze_ResolvesExternalControlTypeOfTarget()
    {
        var analysis = VBCompilation.Create("""
            Sub Apply(ctlControl As Control)
                If TypeOf ctlControl Is CheckBox Then Debug.Print 1
            End Sub

            Sub Main()
                Apply 1
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
    }
}
