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

    [TestMethod]
    public void EmitManagedApplication_ExecutesExtendedScalarMathIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Exp(1)
                Debug.Print Log(Exp(1))
                Debug.Print Sin(0)
                Debug.Print Cos(0)
                Debug.Print Tan(0)
                Debug.Print Atn(1)
            End Sub
            """);

        var expected = new[]
        {
            Math.E.ToString("G15", System.Globalization.CultureInfo.InvariantCulture),
            "1",
            "0",
            "1",
            "0",
            (Math.PI / 4d).ToString("G15", System.Globalization.CultureInfo.InvariantCulture)
        };
        CollectionAssert.AreEqual(expected, output);
    }
}
