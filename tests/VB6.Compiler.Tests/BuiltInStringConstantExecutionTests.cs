namespace VB6.Compiler.Tests;

[TestClass]
public sealed class BuiltInStringConstantExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesVbStringConstants()
    {
        const string source = """
            Sub Main()
                Debug.Print Len(vbCrLf)
                Debug.Print Len(vbTab)
                Debug.Print vbNullString & "x"
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "2", "1", "x" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_UserDeclarationShadowsBuiltInConstant()
    {
        const string source = """
            Private Const vbCrLf As String = "custom"

            Sub Main()
                Debug.Print vbCrLf
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "custom" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_ComposesBuiltInConstantsWithBracketedEnumSymbols()
    {
        const string source = """
            Enum SeparatorLength
                [CrLfLength] = 2
            End Enum

            Sub Main()
                Debug.Print Len(vbCrLf) = [CrLfLength]
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void ProjectAnalysis_ResolvesBuiltInConstantsAcrossModules()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerBuiltInConstantProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Constants.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Constants"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Debug.Print vbCrLf & vbTab
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "VB6S0001" &&
                    (diagnostic.Message.Contains("vbCrLf", StringComparison.OrdinalIgnoreCase) ||
                     diagnostic.Message.Contains("vbTab", StringComparison.OrdinalIgnoreCase))),
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

}
