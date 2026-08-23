namespace VB6.Compiler.Tests;

[TestClass]
public sealed class EnumExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesEnumTypeAliasesAndMemberValues()
    {
        const string source = """
            Enum Fruit
                Apple = 3
                Banana
                Cherry = Apple + 5
            End Enum

            Sub Main()
                Dim value As Fruit
                value = Banana
                Debug.Print value
                Debug.Print Cherry
                Debug.Print Fruit.Apple
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(new[] { "4", "8", "3" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void Analyze_AllowsEnumTypeInsideUserDefinedType()
    {
        var analysis = VBCompilation.Create("""
            Enum SymbolKind
                SymbolNone = 0
                SymbolString = 9
            End Enum

            Type SymbolRecord
                Kind As SymbolKind
            End Type

            Sub Main()
                Dim record As SymbolRecord
                record.Kind = SymbolString
                Debug.Print record.Kind
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic =>
                diagnostic.Code is "VB6S0001" or "VB6S0003" or "VB6S0011"),
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
    }

    [TestMethod]
    public void EmitManagedApplication_SharesEnumAcrossVbpModules()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerEnumProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Enums.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Enums"
                Module=Enums; Enums.bas
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Enums.bas"), """
                Public Enum Status
                    Ready = 10
                    Done
                End Enum
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim state As Status
                    state = Done
                    Debug.Print state
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(new[] { "11" }, VB6TestProgram.SplitLines(standardOutput), standardOutput);
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
