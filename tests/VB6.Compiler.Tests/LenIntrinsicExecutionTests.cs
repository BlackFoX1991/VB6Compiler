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
    public void GenerateCSharp_RewritesBuiltInLenToRuntimeWithoutTouchingUserFunction()
    {
        var builtIn = VBCompilation.Create("""
            Sub Main()
                Debug.Print Len("abc")
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            builtIn.Success,
            string.Join(Environment.NewLine, builtIn.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(builtIn.Source);
        StringAssert.Contains(builtIn.Source, "VBStrings.Len(\"abc\")");
        Assert.IsFalse(builtIn.Diagnostics.Any(diagnostic =>
            diagnostic.Code == "VB6S0005" && diagnostic.Message.Contains("Len", StringComparison.OrdinalIgnoreCase)));

        var userDefined = VBCompilation.Create("""
            Function Len(ByVal value As Long) As Long
                Len = 99
            End Function

            Sub Main()
                Debug.Print Len(1)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            userDefined.Success,
            string.Join(Environment.NewLine, userDefined.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(userDefined.Source);
        StringAssert.Contains(userDefined.Source, "__vb6_Len(");
        Assert.IsFalse(userDefined.Source.Contains("VBStrings.Len(", StringComparison.Ordinal));
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
