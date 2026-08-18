using System.Diagnostics;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ProjectCompilationTests
{
    [TestMethod]
    public void Analyze_CombinesStandardModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory);
            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.AreEqual(2, analysis.Units.Length);
            Assert.IsNotNull(analysis.SemanticModel);
            Assert.AreEqual(4, analysis.SemanticModel!.Procedures.Length);

            var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
            var update = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Update");
            var observe = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Observe");
            var add = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Add");
            var invocations = main.Body.Statements.OfType<BoundInvocationStatement>().ToArray();
            var addAssignment = main.Body.Statements.OfType<BoundAssignmentStatement>().Last();
            var addInvocation = (BoundInvocationExpression)addAssignment.Expression;

            Assert.IsTrue(main.Body.Statements.Any(statement => statement is BoundForStatement));
            Assert.IsTrue(main.Body.Statements.Any(statement => statement is BoundWhileStatement));
            Assert.IsTrue(main.Body.Statements.Count(statement => statement is BoundDoStatement) >= 2);
            Assert.AreEqual(update.Symbol, invocations[0].Procedure);
            Assert.AreEqual(observe.Symbol, invocations[1].Procedure);
            Assert.AreEqual(add.Symbol, addInvocation.Procedure);
            Assert.AreEqual(ParameterPassingMode.ByRef, update.Symbol.Parameters.Single().PassingMode);
            Assert.AreEqual(ParameterPassingMode.ByVal, observe.Symbol.Parameters.Single().PassingMode);
            Assert.AreEqual(TypeSymbol.Integer, add.Symbol.ReturnType);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLoopsCrossModuleCallsAndFunction()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory);
            var outputDirectory = Path.Combine(directory, "bin");
            var assemblyPath = Path.Combine(outputDirectory, "MultiModule.dll");

            var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(assemblyPath);
            Assert.IsTrue(result.Success, FormatDiagnostics(result));
            Assert.IsNotNull(result.AssemblyPath);

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(result.AssemblyPath!);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the generated project application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("12", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsDuplicateProceduresAcrossModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Duplicate.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Duplicate"
                Module=First; First.bas
                Module=Second; Second.bas
                """);
            File.WriteAllText(Path.Combine(directory, "First.bas"), """
                Sub Helper()
                    Debug.Print 1
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Second.bas"), """
                Sub Helper()
                    Debug.Print 2
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(analysis.ProjectDiagnostics.Any(diagnostic => diagnostic.Code == "VB6PRJ0003"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string WriteProject(string directory)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "MultiModule.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="MultiModule"
            Module=MainModule; MainModule.bas
            Module=HelperModule; HelperModule.bas
            """);
        File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
            Option Explicit

            Sub Main()
                Dim x As Integer
                Dim i As Integer
                x = 0

                For i = 1 To 5
                    x = x + 1
                    If i = 3 Then
                        Exit For
                    End If
                Next i

                While x < 5
                    x = x + 1
                Wend

                Do
                    x = x + 1
                    If x = 6 Then
                        Exit Do
                    End If
                Loop

                Do
                    x = x + 1
                Loop Until x = 7

                Call Update(x)
                Call Observe(x)
                x = Add(x, 2)
                Debug.Print x
            End Sub
            """);
        File.WriteAllText(Path.Combine(directory, "HelperModule.bas"), """
            Option Explicit

            Sub Update(value As Integer)
                value = 10
            End Sub

            Sub Observe(ByVal value As Integer)
                value = 20
            End Sub

            Function Add(ByVal left As Integer, ByVal right As Integer) As Integer
                Add = left + right
            End Function
            """);
        return projectPath;
    }

    private static string FormatDiagnostics(VBProjectCompilationAnalysis analysis)
    {
        var projectDiagnostics = analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString());
        var sourceDiagnostics = analysis.Diagnostics.Select(diagnostic => diagnostic.ToString());
        return string.Join(Environment.NewLine, projectDiagnostics.Concat(sourceDiagnostics));
    }

    private static string FormatDiagnostics(VBProjectManagedApplicationEmitResult result)
    {
        var diagnostics = new List<string>
        {
            FormatDiagnostics(result.Generation.Analysis)
        };

        if (result.BackendResult is not null)
        {
            diagnostics.AddRange(result.BackendResult.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Severity} {diagnostic.Id}: {diagnostic.Message}"));
        }

        return string.Join(Environment.NewLine, diagnostics.Where(value => value.Length != 0));
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerProjectTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
