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
    public void EmitManagedApplication_InvokesLongPtrDeclareFunction()
    {
        var output = VB6TestProgram.Run("""
            Private Declare Function GetCurrentProcessId Lib "kernel32" () As LongPtr

            Sub Main()
                Debug.Print CLng(GetCurrentProcessId) > 0
            End Sub
            """);

        Assert.AreEqual("True", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesUIntegerDeclareFunction()
    {
        var output = VB6TestProgram.Run("""
            Private Declare Function GetCurrentProcessId Lib "kernel32" () As UInt32

            Sub Main()
                Debug.Print CLng(GetCurrentProcessId) > 0
            End Sub
            """);

        Assert.AreEqual("True", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesUnsignedWidthDeclareFunctions()
    {
        var output = VB6TestProgram.Run("""
            Private Declare Function GetCurrentProcessId Lib "kernel32" () As UInt16
            Private Declare Function GetCurrentProcessIdWide Lib "kernel32" Alias "GetCurrentProcessId" () As UInt64

            Sub Main()
                Debug.Print CLng(GetCurrentProcessId) > 0
                Debug.Print CBool(CULng(GetCurrentProcessIdWide) > 0)
            End Sub
            """);

        Assert.AreEqual("True" + Environment.NewLine + "True", output.Trim());
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
            Private Declare Function NativeCommandLine Lib "kernel32" Alias "GetCommandLineA" () As String

            Sub Main()
                Debug.Print NativeStringLength("abc")
                Debug.Print Len(NativeCommandLine()) > 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "3", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesScalarAsAnyPointerTransfer()
    {
        var output = VB6TestProgram.Run("""
            Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

            Sub Main()
                Dim source As Long
                Dim destination As Long

                source = 16909060
                CopyMemory destination, source, 4
                Debug.Print destination

                destination = 0
                CopyMemory destination, ByVal VarPtr(source), 4
                Debug.Print destination
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "16909060", "16909060" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesByRefBlittableDeclareUdt()
    {
        var output = VB6TestProgram.Run("""
            Private Type SYSTEMTIME
                wYear As Integer
                wMonth As Integer
                wDayOfWeek As Integer
                wDay As Integer
                wHour As Integer
                wMinute As Integer
                wSecond As Integer
                wMilliseconds As Integer
            End Type

            Private Declare Sub GetSystemTime Lib "kernel32" (ByRef value As SYSTEMTIME)

            Sub Main()
                Dim value As SYSTEMTIME
                GetSystemTime value
                Debug.Print value.wYear >= 2020
                Debug.Print value.wMonth >= 1 And value.wMonth <= 12
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_MarshalsFixedStringFieldsInsideDeclareUdt()
    {
        var output = VB6TestProgram.Run("""
            Private Type OSVERSIONINFO
                dwOSVersionInfoSize As Long
                dwMajorVersion As Long
                dwMinorVersion As Long
                dwBuildNumber As Long
                dwPlatformId As Long
                szCSDVersion As String * 128
            End Type

            Private Declare Function GetVersionExA Lib "kernel32" (ByRef value As OSVERSIONINFO) As Long

            Sub Main()
                Dim value As OSVERSIONINFO
                value.dwOSVersionInfoSize = 148
                Debug.Print GetVersionExA(value) >= 0
                Debug.Print Len(value.szCSDVersion) <= 128
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }
}
