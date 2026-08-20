using System.Diagnostics;
using VB6.ProjectSystem;
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
            Assert.IsTrue(main.Body.Statements.Any(statement => statement is BoundSelectCaseStatement));
            Assert.IsTrue(main.Body.Statements.Count(statement => statement is BoundIfStatement) >= 3);
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
    public void EmitManagedApplication_ExecutesControlFlowCrossModuleCallsAndFunction()
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

    [TestMethod]
    public void Analyze_ReadsClassModulesWithoutExportingThemAsStandardModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ClassSource.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ClassSource"
                Module=MainModule; MainModule.bas
                Class=Customer; Customer.cls
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Debug.Print 1
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Customer.cls"), """
                Private id As Long

                Sub Configure()
                    id = 10
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.AreEqual(2, analysis.Units.Length);
            Assert.IsNotNull(analysis.SemanticModel);
            Assert.AreEqual(1, analysis.SemanticModel!.Procedures.Length);
            Assert.AreEqual("Main", analysis.SemanticModel.Procedures.Single().Symbol.Name);

            var classUnit = analysis.Units.Single(unit => unit.Item.Kind == VBProjectItemKind.Class);
            Assert.IsNotNull(classUnit.Analysis.SemanticModel);
            Assert.AreEqual(1, classUnit.Analysis.SemanticModel!.Procedures.Length);
            Assert.AreEqual("Configure", classUnit.Analysis.SemanticModel.Procedures.Single().Symbol.Name);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ResolvesProjectWideEnumTypesAndConstants()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ProjectEnums.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ProjectEnums"
                Module=TypesModule; TypesModule.bas
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "TypesModule.bas"), """
                Public Enum Alignment
                    AlignLeft = 0
                    AlignCenter = 2
                End Enum
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Private Current As Alignment

                Sub Main()
                    Current = AlignCenter
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.AreEqual(1, analysis.SemanticModel!.EnumTypes.Length);
            Assert.IsTrue(analysis.SemanticModel.ModuleVariables.Any(variable =>
                variable.IsConstant && variable.Symbol.Name == "AlignCenter"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ResolvesClassModulesAsProjectTypes()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ClassTypes.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ClassTypes"
                Module=MainModule; MainModule.bas
                Class=Customer; Domain\Customer.cls
                Class=Invoice.cls
                """);
            Directory.CreateDirectory(Path.Combine(directory, "Domain"));
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Private CurrentCustomer As Customer
                Private CurrentInvoice As Invoice

                Sub Main()
                    Debug.Print 1
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Domain", "Customer.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Customer"

                Private id As Long
                """);
            File.WriteAllText(Path.Combine(directory, "Invoice.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Invoice"

                Private id As Long
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.IsNotNull(analysis.SemanticModel);
            Assert.IsTrue(analysis.SemanticModel!.ClassTypes.Any(type => type.Name == "Customer"));
            Assert.IsTrue(analysis.SemanticModel.ClassTypes.Any(type => type.Name == "Invoice"));

            var customer = analysis.SemanticModel.ModuleVariables.Single(variable =>
                variable.Symbol.Name == "CurrentCustomer");
            var invoice = analysis.SemanticModel.ModuleVariables.Single(variable =>
                variable.Symbol.Name == "CurrentInvoice");
            Assert.IsInstanceOfType<ClassTypeSymbol>(customer.Symbol.Type);
            Assert.AreEqual("Customer", customer.Symbol.Type.Name);
            Assert.IsInstanceOfType<ClassTypeSymbol>(invoice.Symbol.Type);
            Assert.AreEqual("Invoice", invoice.Symbol.Type.Name);
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
                Dim flag As Boolean
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

                Select Case x
                    Case 1 To 6
                        x = 100
                    Case 7, 8
                        x = x
                    Case Is > 8
                        x = 200
                    Case Else
                        x = 300
                End Select

                If x < 0 Then
                    x = 100
                ElseIf x = 7 Then
                    x = 8
                Else
                    x = 200
                End If

                If x = 8 Then x = 9 Else x = 300

                flag = True
                If flag And Not False And (True Xor False) And (True Eqv True) And (False Imp True) Then
                    x = x
                Else
                    x = 300
                End If

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
