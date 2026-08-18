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
            Sub Main()
                missing = 10
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0001"));
    }

    [TestMethod]
    public void GenerateCSharp_ReturnsGeneratedSource()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 10
            End Sub
            """, "Module1.bas");

        var generation = compilation.GenerateCSharp();

        Assert.IsTrue(generation.Success);
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source!, "public static void Main()");
        StringAssert.Contains(generation.Source!, "VBConversions.CInt(10L)");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesRequiredFiles()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 10
                Debug.Print x
            End Sub
            """, "Module1.bas");

        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "GeneratedProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var backendDiagnostics = result.BackendResult is null
                ? string.Empty
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}"));

            Assert.IsTrue(result.Success, backendDiagnostics);
            Assert.IsNotNull(result.AssemblyPath);
            Assert.IsNotNull(result.RuntimeAssemblyPath);
            Assert.IsNotNull(result.RuntimeConfigPath);
            Assert.IsTrue(File.Exists(result.AssemblyPath!));
            Assert.IsTrue(File.Exists(result.RuntimeAssemblyPath!));
            Assert.IsTrue(File.Exists(result.RuntimeConfigPath!));
            Assert.IsTrue(new FileInfo(result.AssemblyPath!).Length > 0);
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
