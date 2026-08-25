namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedBoundsAndArrayParameters()
    {
        const string source = """
            Option Base 1

            Function First(values() As Long) As Long
                First = values(1)
            End Function

            Sub Main()
                Dim values(3) As Long
                values(1) = 42
                values(3) = 99
                Debug.Print values(1)
                Debug.Print values(3)
                Debug.Print First(values)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "42", "99", "42" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesMultidimensionalExplicitBounds()
    {
        const string source = """
            Sub Main()
                Dim grid(-1 To 1, 2 To 3) As Integer
                grid(-1, 2) = 7
                grid(1, 3) = 9
                Debug.Print grid(-1, 2)
                Debug.Print grid(1, 3)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "7", "9" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesMemberArrayBounds()
    {
        const string source = """
            Sub Main()
                Dim values(2 To 4) As Long
                Debug.Print values.LBound
                Debug.Print values.UBound
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "2", "4" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesArrayElementByRef()
    {
        const string source = """
            Sub Increment(ByRef value As Long)
                value = value + 1
            End Sub

            Sub Main()
                Dim values(1 To 2) As Long
                values(1) = 41
                Call Increment(values(1))
                Debug.Print values(1)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "42" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesVariantArrayElementByRef()
    {
        const string source = """
            Sub Replace(ByRef value As Variant)
                value = "changed"
            End Sub

            Sub Main()
                Dim values As Variant
                values = Array("before")
                Call Replace(values(0))
                Debug.Print values(0)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "changed" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesWholeVariantArrayByRef()
    {
        const string source = """
            Sub Replace(ByRef value As Variant)
                value = Array("changed")
            End Sub

            Sub Main()
                Dim values As Variant
                values = Array("before")
                Call Replace(values)
                Debug.Print IsArray(values)
                Debug.Print values(0)
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "True", "changed" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesParamArrayWithEmptyAndMixedArguments()
    {
        const string source = """
            Sub Show(ParamArray values() As Variant)
                Dim item As Variant
                For Each item In values
                    Debug.Print item
                Next item
                Debug.Print UBound(values)
            End Sub

            Sub Main()
                Show
                Show 10, "two", True
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "-1", "10", "two", "True", "2" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesArrayIntrinsicWithEmptyAndMixedValues()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim values As Variant

                values = Array()
                Debug.Print IsArray(values)
                Debug.Print UBound(values)

                values = Array(1, "two", True)
                Debug.Print IsArray(values)
                Debug.Print VarType(values)
                Debug.Print values(0)
                Debug.Print values(1)
                Debug.Print values(2)
                Debug.Print UBound(values)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "-1", "True", "8204", "1", "two", "True", "2" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_WritesVariantArrayElements()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim values As Variant

                values = Array(1, "two", True)
                values(0) = 42
                values(1) = "changed"
                values(2) = False
                Debug.Print values(0)
                Debug.Print values(1)
                Debug.Print values(2)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "42", "changed", "False" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesChooseWithRoundedAndOutOfRangeIndexes()
    {
        var output = VB6TestProgram.RunLines("""
            Function NextValue() As Long
                Static value As Long
                value = value + 1
                NextValue = value
            End Function

            Sub Main()
                Debug.Print Choose(1, NextValue(), NextValue(), NextValue())
                Debug.Print NextValue()
                Debug.Print Choose(1, "one", "two", "three")
                Debug.Print Choose(2.6, "one", "two", "three")
                Debug.Print IsNull(Choose(0, "one", "two", "three"))
                Debug.Print IsNull(Choose(4, "one", "two", "three"))
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "1", "4", "one", "three", "True", "True" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesSwitchWithVariantNullWhenNoConditionMatches()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print IsNull(Switch(False, "first", False, "second"))
                Debug.Print Switch(True, "selected", False, "ignored")
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "selected" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesJoinAndFilterForStringArrays()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim words() As String
                Dim filtered() As String

                words = Split("alpha,beta,BETA,gamma", ",")
                Debug.Print Join(words, "-")
                Debug.Print Join(words)

                filtered = Filter(words, "beta")
                Debug.Print UBound(filtered)
                Debug.Print filtered(0)

                filtered = Filter(words, "beta", False, 1)
                Debug.Print UBound(filtered)
                Debug.Print filtered(0)
                Debug.Print filtered(1)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "alpha-beta-BETA-gamma", "alpha beta BETA gamma", "0", "beta", "1", "alpha", "gamma" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesStaticScalarStringAndArrayValues()
    {
        const string source = """
            Function NextValue() As Long
                Static count As Long
                count = count + 1
                NextValue = count
            End Function

            Sub AddText()
                Static text As String
                text = text & "x"
                Debug.Print text
            End Sub

            Sub KeepArray()
                Static values(1 To 2) As Long
                values(1) = values(1) + 1
                Debug.Print values(1)
            End Sub

            Sub Main()
                Debug.Print NextValue()
                Debug.Print NextValue()
                AddText
                AddText
                KeepArray
                KeepArray
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "1", "2", "x", "xx", "1", "2" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
