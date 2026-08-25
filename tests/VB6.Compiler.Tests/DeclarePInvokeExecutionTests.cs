using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VB6.Emit.Managed;
using VB6.Runtime;

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
    public void Lower_UsesSafeArrayBufferForByRefDeclareArray()
    {
        const string source = """
            Private Declare Sub NativeArray Lib "legacy.dll" (ByRef values() As Long)

            Sub Main()
                Dim values(0 To 1) As Long
                NativeArray values
            End Sub
            """;
        var program = VB6TestIr.Lower(source);

        var call = VB6TestIr.Procedures(program)
            .SelectMany(procedure => procedure.Blocks)
            .SelectMany(block => block.Instructions)
            .OfType<VB6.IR.IrEvaluateInstruction>()
            .Select(evaluate => evaluate.Expression)
            .OfType<VB6.IR.IrProcedureCallExpression>()
            .Single();

        Assert.AreEqual(VB6.IR.IrCallArgumentKind.ArrayBuffer, call.Arguments.Single().Kind);

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerSafeArrayDeclareTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var result = VBCompilation.Create(source).EmitManagedApplication(
                Path.Combine(directory, "Program.dll"));
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
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
    public void EmitManagedApplication_InvokesScalarCurrencyDeclare()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The scalar Currency Declare test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Declare Function VarCyFromR8 Lib "oleaut32" (ByVal inputValue As Double, ByRef outputValue As Currency) As Long

            Sub Main()
                Dim value As Currency
                Dim status As Long
                status = VarCyFromR8(12.3456, value)
                Debug.Print status = 0
                Debug.Print value = CCur(12.3456)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesByRefVariantDeclare()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The ByRef Variant Declare test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Declare Function VariantChangeType Lib "oleaut32" (ByRef destination As Variant, ByRef source As Variant, ByVal flags As Integer, ByVal targetType As Integer) As Long

            Sub Main()
                Dim source As Variant
                Dim destination As Variant
                Dim status As Long
                source = 12.5
                status = VariantChangeType(destination, source, 0, 5)
                Debug.Print status = 0
                Debug.Print VarType(destination) = 5
                Debug.Print CDbl(destination) = 12.5
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesVariantBoolForDeclareBoolean()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The Declare Boolean ABI test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Declare Function VarBoolFromI4 Lib "oleaut32" (ByVal inputValue As Long, ByRef outputValue As Boolean) As Long

            Sub Main()
                Dim value As Boolean
                Dim status As Long
                status = VarBoolFromI4(-1, value)
                Debug.Print status = 0
                Debug.Print value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, VB6TestProgram.SplitLines(output), output);
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
            using var process = Process.Start(new ProcessStartInfo(VB6TestProgram.DotnetHostPath)
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
    public void EmitManagedApplication_EmitsNativeWidthLongPtrDeclareArrayForSelectedPlatform()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerLongPtrDeclareArrayTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var assemblyPath = Path.Combine(directory, "LongPtrDeclareArray.dll");
        var platform = Environment.Is64BitProcess ? ManagedPlatform.X64 : ManagedPlatform.X86;
        var expectedElementType = Environment.Is64BitProcess ? VarEnum.VT_I8 : VarEnum.VT_I4;

        try
        {
            var result = VBCompilation.Create("""
                Private Declare Sub NativeLongPtrArray Lib "kernel32" (ByRef values() As LongPtr)

                Sub Main()
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                    assemblyPath,
                    new ManagedEmitOptions("LongPtrDeclareArray", Platform: platform));

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            var assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            var method = assembly
                .GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Single(candidate => candidate.Name == "NativeLongPtrArray");
            var parameter = method.GetParameters().Single();
            Assert.AreEqual(typeof(IntPtr), parameter.ParameterType);
            var marshal = parameter.GetCustomAttribute<MarshalAsAttribute>();
            Assert.AreEqual(UnmanagedType.SafeArray, marshal?.Value);
            Assert.AreEqual(expectedElementType, marshal?.SafeArraySubType);
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
    public void EmitManagedApplication_RejectsAnyCpuLongPtrDeclareArray()
    {
        var result = VBCompilation.Create("""
            Private Declare Sub NativeLongPtrArray Lib "kernel32" (ByRef values() As LongPtr)

            Sub Main()
            End Sub
            """, "Module1.bas").EmitManagedApplication(
                Path.Combine(
                    Path.GetTempPath(),
                    "VB6CompilerLongPtrDeclareArrayTests",
                    Guid.NewGuid().ToString("N"),
                    "AnyCpu.dll"),
                new ManagedEmitOptions("AnyCpuLongPtrDeclareArray"));

        Assert.IsFalse(result.Success);
        var diagnostics = result.Diagnostics
            .Select(diagnostic => diagnostic.ToString())
            .Concat(result.BackendResult?.Diagnostics.Select(diagnostic => diagnostic.Message) ?? []);
        StringAssert.Contains(
            string.Join(Environment.NewLine, diagnostics),
            "LongPtr() SAFEARRAY contracts require an explicit --x86 or --x64 target");
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
    public void EmitManagedApplication_UsesTemporaryUtf16BufferForByValStrPtrAsAny()
    {
        var output = VB6TestProgram.Run("""
            Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

            Sub Main()
                Dim source As String
                Dim destination As String
                source = "ABCD"
                destination = "...."

                CopyMemory ByVal StrPtr(destination), ByVal StrPtr(source), 8
                Debug.Print destination
            End Sub
            """);

        Assert.AreEqual("ABCD", output.Trim());
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

    [TestMethod]
    public void EmitManagedApplication_WritesBackVariableDeclareStringBuffers()
    {
        var output = VB6TestProgram.Run("""
            Private Declare Function GetComputerNameA Lib "kernel32" (ByVal lpBuffer As String, nSize As Long) As Long

            Sub Main()
                Dim buffer As String
                Dim size As Long
                buffer = String(256, 0)
                size = 256
                Debug.Print GetComputerNameA(buffer, size) <> 0
                Debug.Print size > 0 And Left$(buffer, size) <> ""
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesVb6FourByteUdtPackingForDeclarePointers()
    {
        var output = VB6TestProgram.Run("""
            Private Type MixedValue
                prefix As Byte
                value As Double
            End Type

            Private Type RawBytes
                first As Long
                second As Long
                third As Long
            End Type

            Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

            Sub Main()
                Dim source As MixedValue
                Dim destination As RawBytes
                source.prefix = 65
                source.value = 1.5
                CopyMemory destination, source, 12
                Debug.Print destination.first = 65
                Debug.Print destination.second = 0
                Debug.Print destination.third = 1073217536
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }
}
