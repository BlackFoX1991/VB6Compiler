using VB6.Semantics;
using VB6.Syntax.Nodes;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ForEachProjectCompilationTests
{
    [TestMethod]
    public void EmitManagedApplication_LowersFixedArrayForEachInsideVbpProject()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Sub Main()
                    Dim item
                    Dim values(1 To 2, 5 To 6) As Long
                    values(1, 5) = 10
                    values(1, 6) = 11
                    values(2, 5) = 20
                    values(2, 6) = 21

                    For Each item In values
                        Debug.Print item
                    Next item
                End Sub
                """);
            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "10", "11", "20", "21" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
                standardOutput);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ProjectPathDefaultsUntypedDeclarationsToVariantWithoutMutatingParseTree()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Public Current

                Sub Main()
                    Dim item
                    Dim values(1 To 2)
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(
                analysis.Success,
                string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0020"));
            Assert.IsNotNull(analysis.SemanticModel);

            var current = analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "Current");
            Assert.AreSame(TypeSymbol.Variant, current.Symbol.Type);

            var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
            Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "item").Type);
            var values = (ArrayTypeSymbol)main.Locals.Single(local => local.Name == "values").Type;
            Assert.AreSame(TypeSymbol.Variant, values.ElementType);

            var parsedSub = (SubDeclarationSyntax)analysis.Units.Single().Analysis.ParseResult.Root.Members
                .Single(member => member is SubDeclarationSyntax);
            var parsedItem = ((DimStatementSyntax)parsedSub.Statements[0]).Declarators.Single();
            Assert.IsNull(parsedItem.TypeToken);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ProjectForEachPreservesControlVariableGuard()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Sub Main()
                    Dim item As Long
                    Dim values(1 To 2) As Long
                    For Each item In values
                        Debug.Print item
                    Next item
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            CollectionAssert.Contains(
                analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                "VB6S0054");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_EnumeratesHostObjectWithObjectControlVariable()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim host As Object
                For Each item In host
                    Debug.Print item
                Next item
            End Sub
            """);

        Assert.AreEqual(string.Empty, output);
    }

    [TestMethod]
    public void Analyze_ProjectPathAllowsVariantArithmetic()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Sub Main()
                    Dim value As Variant
                    Debug.Print value + 1
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string WriteProject(string directory, string moduleSource)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "ForEachProject.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="ForEachProject"
            Module=MainModule; MainModule.bas
            """);
        File.WriteAllText(Path.Combine(directory, "MainModule.bas"), moduleSource);
        return projectPath;
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerForEachProjectTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
