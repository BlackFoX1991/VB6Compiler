namespace VB6.Compiler.Tests;

/// <summary>
/// The documented math surface and, with it, how many digits VB6 shows. A Single carries seven
/// significant digits and a Double fifteen; printing both with fifteen shows a Single's
/// representation error as if it were a value.
/// </summary>
[TestClass]
public sealed class MathSurfaceExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_AppliesTheDocumentedMathContracts()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                ' Int rundet zur nächstkleineren Zahl, Fix schneidet zur Null hin ab -- der
                ' Unterschied zeigt sich nur bei negativen Werten.
                Debug.Print Int(-8.4) & "," & Fix(-8.4)
                Debug.Print Int(8.4) & "," & Fix(8.4)
                Debug.Print Sgn(-3) & "," & Sgn(0) & "," & Sgn(3)
                Debug.Print Abs(-3.5)
                Debug.Print Sqr(9)
                Debug.Print Log(1)
                Debug.Print Exp(0)

                ' Ganzzahldivision schneidet ab, Mod rundet seine Operanden vorher.
                Debug.Print (7 \ 2) & "," & (-7 \ 2)
                Debug.Print (7 Mod 3) & "," & (-7 Mod 3) & "," & (7.6 Mod 3)
                Debug.Print (2 ^ 10) & "," & (2 ^ -1)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "-9,-8", "8,8", "-1,0,1", "3.5", "3", "0", "1", "3,-3", "1,-1,2", "1024,0.5" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsTheDocumentedMathErrors()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim d As Double
                d = Sqr(-1)
                Debug.Print Err.Number
                Err.Clear
                d = Log(0)
                Debug.Print Err.Number
                Err.Clear
                d = 1 / 0
                Debug.Print Err.Number
            End Sub
            """);

        // 5 für ein ungültiges Argument, 11 für die Division durch null.
        CollectionAssert.AreEqual(new[] { "5", "5", "11" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ShowsSevenDigitsForSingleAndFifteenForDouble()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim d As Double
                d = Atn(1) * 4
                Debug.Print d
                Debug.Print CStr(d)

                ' 1 / 3 ist in VB6 ein Single: beide Operanden sind Integer.
                Debug.Print 1 / 3
                Debug.Print CStr(1 / 3)

                Dim genau As Double
                genau = 1
                genau = genau / 3
                Debug.Print genau
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "3.14159265358979",
                "3.14159265358979",
                "0.3333333",
                "0.3333333",
                "0.333333333333333"
            },
            output);
    }
}
