namespace VB6.Compiler.Tests;

[TestClass]
public sealed class MathIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesScalarMathIntrinsics()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print Abs(-12)
                Debug.Print Sgn(-0.5)
                Debug.Print Fix(-1.8)
                Debug.Print Round(2.5)
                Debug.Print Sqr(9)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "12", "-1", "-1", "2", "3" }, VB6TestProgram.SplitLines(output), output);
    }
}
