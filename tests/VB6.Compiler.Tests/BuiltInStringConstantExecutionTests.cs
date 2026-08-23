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
    public void EmitManagedApplication_UsesNumericVbConstants()
    {
        const string source = """
            Sub Main()
                Debug.Print vbWhite
                Debug.Print vbBlack
                Debug.Print vbButtonFace
                Debug.Print vbGrayText
                Debug.Print vbPicTypeBitmap
                Debug.Print tvwChild
                Debug.Print BF_RECT
                Debug.Print EDGE_RAISED
                Debug.Print vbSrcCopy
                Debug.Print vbRetry
                Debug.Print vbIgnore
                Debug.Print vbAltMask
                Debug.Print vbNormalFocus
                Debug.Print vbSolid
                Debug.Print vbTrue
                Debug.Print vbObjectError
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "16777215", "0", "-2147483633", "-2147483631", "0", "4", "15", "5", "13369376", "4", "5", "4", "1", "0", "-1", "-2147221504" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_UserDeclarationShadowsExternalConstant()
    {
        const string source = """
            Private Const tvwChild As Long = 99

            Sub Main()
                Debug.Print tvwChild
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "99" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_UserDeclarationShadowsNumericBuiltInConstant()
    {
        const string source = """
            Private Const vbWhite As Long = 42

            Sub Main()
                Debug.Print vbWhite
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "42" }, VB6TestProgram.SplitLines(output), output);
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
