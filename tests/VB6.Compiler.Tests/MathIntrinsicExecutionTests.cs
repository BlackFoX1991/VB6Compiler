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

    [TestMethod]
    public void EmitManagedApplication_PreservesNullAndEmptyMathSemantics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = Null

                Debug.Print IsNull(Abs(value))
                Debug.Print IsNull(Sgn(value))
                Debug.Print IsNull(Fix(value))
                Debug.Print IsNull(Round(value))
                Debug.Print Sgn(Empty)
                Debug.Print Abs(Empty)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True", "True", "0", "0" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesVariantStateForInt()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = Null

                Debug.Print IsNull(Int(value))
                Debug.Print Int(Empty)
                Debug.Print Int(CDate(43832.75))

                On Error Resume Next
                Debug.Print Int(Missing)
                Debug.Print Err.Number
                On Error GoTo 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "0", "43832", "448" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesMissingAndArrayMathErrorContracts()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                Dim values As Variant
                value = Missing
                values = Array(1)

                On Error Resume Next
                Debug.Print Abs(value)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print Fix(value)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print Round(value)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print Abs(values)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print Fix(values)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print Round(values)
                Debug.Print Err.Number
                On Error GoTo 0
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "448", "448", "448", "13", "13", "13" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesRndAndRandomizeContracts()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim first As Single

                Debug.Print Rnd >= 0
                Debug.Print Rnd < 1
                Debug.Print Rnd(0) = Rnd(0)
                Debug.Print Rnd(-1) = Rnd(-1)

                Randomize 1
                first = Rnd
                Rnd -1
                Randomize 1
                Debug.Print first = Rnd
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True", "True", "True" }, output);
    }
}
