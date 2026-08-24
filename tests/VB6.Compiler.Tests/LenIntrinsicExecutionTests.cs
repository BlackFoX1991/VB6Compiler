namespace VB6.Compiler.Tests;

[TestClass]
public sealed class LenIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesLenForStringEmptyAndIntegerVariant()
    {
        const string source = """
            Sub Main()
                Dim value
                Debug.Print Len("Hello")
                Debug.Print Len(value)
                value = 42
                Debug.Print Len(value)
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "5", "0", "2" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLenForDateVariant()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim value
                value = CDate(43832)
                Debug.Print Len(value)
            End Sub
            """);

        Assert.AreEqual("8", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLenForPackedUserDefinedType()
    {
        var output = VB6TestProgram.Run("""
            Type MixedValue
                Prefix As Byte
                Value As Double
            End Type

            Sub Main()
                Dim value As MixedValue
                Debug.Print Len(value)
            End Sub
            """);

        Assert.AreEqual("12", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLenForFixedStringUserDefinedType()
    {
        var output = VB6TestProgram.Run("""
            Type FixedValue
                Text As String * 5
                Count As Long
            End Type

            Sub Main()
                Dim value As FixedValue
                Debug.Print Len(value)
            End Sub
            """);

        Assert.AreEqual("12", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLenBForUnicodeScalarAndNullValues()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim value As Variant
                Debug.Print LenB("Hello")
                value = CInt(42)
                Debug.Print LenB(value)
                Debug.Print IsNull(LenB(Null))
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "10", "2", "True" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLenBForPackedUserDefinedType()
    {
        var output = VB6TestProgram.Run("""
            Type MixedValue
                Prefix As Byte
                Value As Double
            End Type

            Sub Main()
                Dim value As MixedValue
                Debug.Print LenB(value)
            End Sub
            """);

        Assert.AreEqual("12", output.Trim());
    }

    [TestMethod]
    /// <summary>A user-defined Len shadows the intrinsic, exactly as in VB6.</summary>
    public void EmitManagedApplication_PrefersAUserFunctionOverTheIntrinsicLen()
    {
        var output = VB6TestProgram.Run("""
            Function Len(ByVal value As Long) As Long
                Len = 99
            End Function

            Sub Main()
                Debug.Print Len(1)
            End Sub
            """);

        Assert.AreEqual("99", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLenInsideVbpProject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerLenProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "LenProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="LenProject"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim value
                    Debug.Print Len("project")
                    Debug.Print Len(value)
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "7", "0" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

}
