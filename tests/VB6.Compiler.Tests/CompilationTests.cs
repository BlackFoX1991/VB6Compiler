namespace VB6.Compiler.Tests;

[TestClass]
public sealed class CompilationTests
{
    [TestMethod]
    public void Analyze_AcceptanceProgramPassesFrontEnd()
    {
        var compilation = VBCompilation.Create("""
            Option Explicit

            Sub Main()
                Dim x As Integer
                x = 10

                If x > 5 Then
                    Debug.Print x
                End If
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsTrue(analysis.Success);
        Assert.AreEqual(0, analysis.Diagnostics.Length);
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.AreEqual(1, analysis.SemanticModel!.Procedures.Length);
    }

    [TestMethod]
    public void Analyze_StopsBeforeBindingWhenParsingFails()
    {
        var compilation = VBCompilation.Create("Sub", "broken.bas");

        var analysis = compilation.Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6P")));
    }

    [TestMethod]
    public void Analyze_IncludesSemanticDiagnostics()
    {
        var compilation = VBCompilation.Create("""
            Option Explicit

            Sub Main()
                missing = 10
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0001"));
    }

    /// <summary>
    /// A duplicate name has to stay a reported diagnostic. Binding keeps both declarations after
    /// reporting VB6S0004, so anything downstream that keys procedures by name sees the name twice
    /// - which is how a source-location pass once turned this into an unhandled ArgumentException.
    /// </summary>
    [TestMethod]
    public void Analyze_ReportsDuplicateProcedureInsteadOfThrowing()
    {
        var compilation = VBCompilation.Create("""
            Sub Foo()
                Debug.Print 1
            End Sub

            Sub Foo()
                Debug.Print 2
            End Sub

            Sub Main()
                Foo
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0004"));
    }

    /// <summary>A Sub and a Function may not share a name either, and must not crash the analysis.</summary>
    [TestMethod]
    public void Analyze_ReportsSubAndFunctionSharingAName()
    {
        var compilation = VBCompilation.Create("""
            Sub Foo()
                Debug.Print 1
            End Sub

            Function Foo() As Long
                Foo = 2
            End Function

            Sub Main()
                Debug.Print Foo()
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0004"));
    }


    [TestMethod]
    public void EmitManagedApplication_WritesRequiredFiles()
    {
        var compilation = CreatePrintableCompilation();
        var directory = CreateTemporaryDirectory();
        var assemblyPath = Path.Combine(directory, "GeneratedProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            AssertSuccessfulEmit(result);
            Assert.IsTrue(File.Exists(result.AssemblyPath!));
            Assert.IsTrue(File.Exists(result.RuntimeAssemblyPath!));
            Assert.IsTrue(File.Exists(result.RuntimeConfigPath!));
            Assert.IsTrue(new FileInfo(result.AssemblyPath!).Length > 0);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesGeneratedProgram()
    {
        var standardOutput = VB6TestProgram.Run(CreatePrintableCompilation());

        Assert.AreEqual("10", standardOutput.Trim());
    }

    private static VBCompilation CreatePrintableCompilation() => VBCompilation.Create("""
        Sub Main()
            Dim x As Integer
            x = 10
            Debug.Print x
        End Sub
        """, "Module1.bas");

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerTests", Guid.NewGuid().ToString("N"));

    private static void AssertSuccessfulEmit(ManagedApplicationEmitResult result)
    {
        var backendDiagnostics = result.BackendResult is null
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                result.BackendResult.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

        Assert.IsTrue(result.Success, backendDiagnostics);
        Assert.IsNotNull(result.AssemblyPath);
        Assert.IsNotNull(result.RuntimeAssemblyPath);
        Assert.IsNotNull(result.RuntimeConfigPath);
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
