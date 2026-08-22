using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DeclarePInvokeExecutionTests
{
    [TestMethod]
    public void Lower_RegistersScalarDeclareFunctionsAsExternalProcedures()
    {
        var lowering = VBCompilation.Create("""
            Private Declare Function GetCurrentProcessId Lib "kernel32" () As Long

            Sub Main()
                Debug.Print GetCurrentProcessId
            End Sub
            """).Lower();

        Assert.IsTrue(lowering.Success, string.Join(Environment.NewLine, lowering.Diagnostics));
        var external = lowering.Program!.Modules
            .SelectMany(module => module.Procedures)
            .Single(procedure => procedure.Name == "GetCurrentProcessId");

        Assert.IsTrue(external.IsExternal);
        Assert.AreEqual("kernel32", external.ExternalLibrary);
        Assert.IsNull(external.ExternalAlias);
    }

    [TestMethod]
    public void EmitManagedApplication_WritesPInvokeMethodImportMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerPInvokeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var assemblyPath = Path.Combine(directory, "Program.dll");

        try
        {
            var result = VBCompilation.Create("""
                Private Declare Function GetCurrentProcessId Lib "kernel32" () As Long

                Sub Main()
                    Debug.Print GetCurrentProcessId
                End Sub
                """).EmitManagedApplication(assemblyPath);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var metadata = peReader.GetMetadataReader();
            var methodHandle = metadata.MethodDefinitions
                .Single(handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == "GetCurrentProcessId");
            var method = metadata.GetMethodDefinition(methodHandle);
            Assert.IsTrue((method.Attributes & MethodAttributes.PinvokeImpl) != 0);

            var import = method.GetImport();
            Assert.AreEqual("GetCurrentProcessId", metadata.GetString(import.Name));
            Assert.AreEqual("kernel32", metadata.GetString(metadata.GetModuleReference(import.Module).Name));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesScalarDeclareFunction()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerPInvokeExecutionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var assemblyPath = Path.Combine(directory, "Program.dll");

        try
        {
            var result = VBCompilation.Create("""
                Private Declare Function GetCurrentProcessId Lib "kernel32" () As Long

                Sub Main()
                    Debug.Print GetCurrentProcessId
                End Sub
                """).EmitManagedApplication(assemblyPath);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            using var process = Process.Start(new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { assemblyPath }
            });
            Assert.IsNotNull(process);

            if (!process!.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                Assert.Fail(
                    "Generated P/Invoke program did not exit within three seconds. " +
                    $"stdout='{process.StandardOutput.ReadToEnd()}', stderr='{process.StandardError.ReadToEnd()}'.");
            }

            var standardError = process.StandardError.ReadToEnd();
            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.IsTrue(int.TryParse(VB6TestProgram.SplitLines(process.StandardOutput.ReadToEnd()).Single(), out var processId));
            Assert.IsTrue(processId > 0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedApplication_AcceptsAnsiDeclareStringMarshalling()
    {
        var result = VBCompilation.Create("""
            Private Declare Function GetModuleHandle Lib "kernel32" Alias "GetModuleHandleA" (ByVal moduleName As String) As Long

            Sub Main()
                Debug.Print 1
            End Sub
            """).EmitManagedApplication(
                Path.Combine(Path.GetTempPath(), "VB6CompilerPInvokeTests", Guid.NewGuid().ToString("N"), "Program.dll"));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesAnsiDeclareString()
    {
        var output = VB6TestProgram.Run("""
            Private Declare Function NativeStringLength Lib "kernel32" Alias "lstrlenA" (ByVal value As String) As Long

            Sub Main()
                Debug.Print NativeStringLength("abc")
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "3" }, VB6TestProgram.SplitLines(output), output);
    }
}
