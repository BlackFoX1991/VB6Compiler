using VB6.IR;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantConcatenationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ConcatenatesEmptyNumericAndStringVariants()
    {
        const string source = """
            Sub Main()
                Dim value As Variant
                Debug.Print value & "x"
                value = 42
                Debug.Print value & "x"
                value = "a"
                Debug.Print "x" & value
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "x", "42x", "xa" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void Lower_AllowsOnlyBoundAmpersandStringPath()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim value As Variant
                Debug.Print value & "x"
            End Sub
            """);

        // Variant operands use the dedicated object-based concatenation path so Null can be
        // treated as an empty string without changing the explicit CStr(Null) error behavior.
        CollectionAssert.IsSubsetOf(
            new[] { IrRuntimeMethod.ConcatVariant },
            VB6TestIr.RuntimeCalls(program).ToArray());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesVariantArithmeticOperators()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = 10
                Debug.Print value + 2
                Debug.Print value - 3
                Debug.Print value / 4
                Debug.Print value \ 4
                Debug.Print value Mod 4
                Debug.Print value ^ 2
                Debug.Print -value
                Debug.Print Not value
                Debug.Print value And 3
                Debug.Print value Or 3
                Debug.Print value Xor 3
                Debug.Print value Eqv 3
                Debug.Print value Imp 3
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "12", "7", "2.5", "2", "2", "100", "-10", "-11", "2", "11", "9", "-10", "-9" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesVb6AdditionStringRulesForVariants()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim left As Variant
                Dim right As Variant

                left = "a"
                right = "b"
                Debug.Print left + right
                right = 1
                Debug.Print "x" + right
                left = 1
                Debug.Print left + "x"

                right = Null
                Debug.Print "x" + right
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "ab", "x1", "1x", "Null" }, output);
    }

    [TestMethod]
    public void ProjectAnalysis_AllowsVariantAmpersandConcatenation()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantConcatProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Concat.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Concat"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim value As Variant
                    value = 42
                    Debug.Print "value=" & value
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"),
                string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ConcatenatesNullVariantAsEmptyString()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = Null
                Debug.Print value & "x"
                Debug.Print "x" & value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "x", "x" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PropagatesNullWhenBothVariantOperandsAreNull()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim left As Variant
                Dim right As Variant
                left = Null
                right = Null

                Debug.Print IsNull(left & right)
                Debug.Print TypeName(left & right)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "Null" }, output);
    }


    [TestMethod]
    public void EmitManagedApplication_SelectsTheDocumentedVariantPromotionSubtype()
    {
        // Die Promotionstabelle aus der VB6-Dokumentation, Zeile fuer Zeile: erst der
        // gewaehlte Subtyp (VarType), dann der Wert.
        //
        // Die Operanden laufen bewusst ueber Variant-Variablen. Inline geschrieben waere
        // "CInt(32767) + CInt(1)" ein statisch typisierter Integer-Ausdruck und muesste
        // nach der Projektinvariante ueberlaufen; die Promotionsstufen -- Integer nach
        // Long, Long nach Double, Byte nach Integer -- gelten nur fuer Variant-Operanden.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim a As Variant
                Dim b As Variant
                Dim c As Variant

                a = Empty
                b = Empty
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = Empty
                b = CInt(3)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = Empty
                b = "ab"
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(32767)
                b = CInt(1)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CLng(2147483647)
                b = CLng(1)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CByte(200)
                b = CByte(100)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(-32768)
                b = CInt(1)
                c = a - b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(300)
                b = CInt(300)
                c = a * b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(2)
                b = CLng(3)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(2)
                b = CSng(1.5)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(2)
                b = CDbl(1.5)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CCur(2)
                b = CLng(3)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CCur(2)
                b = CDbl(1.5)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CDec("2")
                b = CInt(3)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = "5"
                b = CInt(3)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(3)
                b = "5"
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(7)
                b = CInt(2)
                c = a / b
                Debug.Print VarType(c)
                Debug.Print c
                a = CCur(7)
                b = CCur(2)
                c = a / b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(7)
                b = CInt(2)
                c = a \ b
                Debug.Print VarType(c)
                Debug.Print c
                a = CDbl(7.6)
                b = CDbl(2.2)
                c = a \ b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(7)
                b = CInt(2)
                c = a Mod b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(2)
                b = CInt(10)
                c = a ^ b
                Debug.Print VarType(c)
                Debug.Print c
                a = True
                b = True
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
                a = True
                b = CInt(3)
                c = a + b
                Debug.Print VarType(c)
                Debug.Print c
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "2", "0", "2", "3", "8", "ab",
                "3", "32768", "5", "2147483648", "2", "300",
                "3", "-32769", "3", "90000", "3", "5",
                "4", "3.5", "5", "3.5", "6", "5",
                "5", "3.5", "14", "5", "5", "8",
                "5", "8", "4", "3.5", "5", "3.5",
                "2", "3", "3", "4", "2", "1",
                "5", "1024", "2", "-2", "2", "2"
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsVariantComparisonLogicalAndConcatContracts()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim a As Variant
                Dim b As Variant
                Dim c As Variant

                a = CInt(3)
                b = CInt(5)
                c = a & b
                Debug.Print VarType(c)
                Debug.Print c
                a = Empty
                b = Empty
                c = a & b
                Debug.Print VarType(c)
                a = True
                b = False
                c = a And b
                Debug.Print VarType(c)
                Debug.Print c
                a = CInt(6)
                b = CInt(3)
                c = a And b
                Debug.Print VarType(c)
                Debug.Print c
                Debug.Print (a Or b)
                Debug.Print (a Xor b)
                Debug.Print (a Eqv b)
                Debug.Print (a Imp b)
                a = CInt(3)
                b = "3"
                Debug.Print (a = b)
                a = Empty
                b = CInt(0)
                Debug.Print (a = b)
                b = ""
                Debug.Print (a = b)
            End Sub
            """);

        // "&" liefert immer String, Boolean And Boolean bleibt Boolean (11), waehrend
        // Integer And Integer bitweise auf Integer (2) rechnet.
        CollectionAssert.AreEqual(
            new[]
            {
                "8", "35", "8", "11", "False", "2", "2",
                "7", "5", "-6", "-5",
                "True", "True", "True"
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_RejectsIncompatibleVariantOperands()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim a As Variant
                Dim b As Variant
                Dim c As Variant

                On Error Resume Next
                a = "abc"
                b = CInt(3)
                c = a + b
                Debug.Print Err.Number
                Err.Clear
                c = a * b
                Debug.Print Err.Number
                Err.Clear
                a = CVErr(5)
                c = a + b
                Debug.Print Err.Number
                Err.Clear
                c = a & "x"
                Debug.Print Err.Number
                Err.Clear
                a = Null
                Debug.Print IsNull(a + b)
                Debug.Print Err.Number
            End Sub
            """);

        // Ein nicht numerischer String und ein Error-Variant melden 13; Null bleibt
        // dagegen ein Wert und wird weitergereicht, statt einen Fehler auszuloesen.
        CollectionAssert.AreEqual(
            new[] { "13", "13", "13", "13", "True", "0" },
            output);
    }
}
