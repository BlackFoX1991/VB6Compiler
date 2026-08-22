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

}
