using System.Diagnostics;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

/// <summary>
/// The general managed path must not hand out a moving CLR interior pointer. The x86 backend has
/// one narrower exception: a stored <c>VarPtr</c> for a local <c>Long</c> uses a native cell. The
/// default AnyCPU path and every remaining storage family still report the explicit VB6 error 5;
/// immediate <c>ByVal … As Any</c> Declare arguments are lowered as call-scoped addresses.
/// </summary>
[TestClass]
public sealed class PointerIntrinsicTests
{
    [TestMethod]
    public void EmitManagedApplication_ReportsWhyVarPtrCannotAnswer()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim zahl As Long
                Dim zeiger As Long
                zahl = 7
                zeiger = VarPtr(zahl)
                Debug.Print Err.Number
                Debug.Print Err.Description
                Err.Clear
                Dim text As String
                text = "abc"
                zeiger = StrPtr(text)
                Debug.Print Err.Number
            End Sub
            """);

        // Die Nummer bleibt VB6s 5 für einen ungültigen Aufruf -- die Beschreibung sagt jetzt,
        // warum, statt den Sammelwert unerklärt zu lassen.
        Assert.AreEqual("5", output[0]);
        StringAssert.Contains(output[1], "ByVal As Any");
        Assert.AreEqual("5", output[2]);
    }

    [TestMethod]
    public void EmitManagedApplication_AnswersObjPtrForAnObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("ObjPtr is the COM identity pointer and needs Windows.");
            return;
        }

        // ObjPtr ist anders gelagert: Die COM-Identität eines Objekts ist stabil und braucht keine
        // festgehaltene Speicherzelle.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim o As Object
                Set o = New Collection
                Debug.Print (ObjPtr(o) <> 0)
                Debug.Print (ObjPtr(Nothing) = 0)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, output);
    }

    [TestMethod]
    public void EmitX86Application_KeepsAStoredLocalLongVarPtrSynchronizedWithNativeWrites()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The stored VarPtr regression uses the Windows RtlMoveMemory probe.");
            return;
        }

        var dotnetHost = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "dotnet",
            "dotnet.exe");
        if (!File.Exists(dotnetHost))
        {
            Assert.Inconclusive("The x86 .NET host required by the x86 VarPtr contract is unavailable.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerStoredVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "StoredVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Sub Main()
                    Dim source As Long
                    Dim destination As Long
                    Dim pointer As Long
                    Dim raw As Integer

                    source = 16909060
                    pointer = VarPtr(source)
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    source = 123
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    destination = 84281096
                    CopyMemory ByVal pointer, destination, 4
                    Debug.Print source

                    Dim shortSource As Integer
                    Dim shortDestination As Integer
                    Dim shortPointer As Long
                    shortSource = 1690
                    shortPointer = VarPtr(shortSource)
                    CopyMemory shortDestination, ByVal shortPointer, 2
                    Debug.Print shortDestination

                    shortSource = 123
                    CopyMemory shortDestination, ByVal shortPointer, 2
                    Debug.Print shortDestination

                    shortDestination = 8428
                    CopyMemory ByVal shortPointer, shortDestination, 2
                    Debug.Print shortSource

                    Dim byteSource As Byte
                    Dim byteDestination As Byte
                    Dim bytePointer As Long
                    byteSource = 169
                    bytePointer = VarPtr(byteSource)
                    CopyMemory byteDestination, ByVal bytePointer, 1
                    Debug.Print byteDestination

                    byteSource = 123
                    CopyMemory byteDestination, ByVal bytePointer, 1
                    Debug.Print byteDestination

                    byteDestination = 42
                    CopyMemory ByVal bytePointer, byteDestination, 1
                    Debug.Print byteSource

                    Dim booleanSource As Boolean
                    Dim booleanPointer As Long
                    booleanSource = True
                    booleanPointer = VarPtr(booleanSource)
                    CopyMemory raw, ByVal booleanPointer, 2
                    Debug.Print raw

                    booleanSource = False
                    CopyMemory raw, ByVal booleanPointer, 2
                    Debug.Print raw

                    raw = 1
                    CopyMemory ByVal booleanPointer, raw, 2
                    Debug.Print booleanSource

                    Dim singleSource As Single
                    Dim singlePointer As Long
                    singleSource = 1.5
                    singlePointer = VarPtr(singleSource)
                    CopyMemory destination, ByVal singlePointer, 4
                    Debug.Print destination

                    singleSource = 2.5
                    CopyMemory destination, ByVal singlePointer, 4
                    Debug.Print destination

                    destination = 1077936128
                    CopyMemory ByVal singlePointer, destination, 4
                    Debug.Print singleSource

                    Dim doubleSource As Double
                    Dim doubleDestination As Double
                    Dim doublePointer As Long
                    doubleSource = 1.5
                    doublePointer = VarPtr(doubleSource)
                    CopyMemory doubleDestination, ByVal doublePointer, 8
                    Debug.Print doubleDestination

                    doubleSource = 2.5
                    CopyMemory doubleDestination, ByVal doublePointer, 8
                    Debug.Print doubleDestination

                    doubleDestination = 3
                    CopyMemory ByVal doublePointer, doubleDestination, 8
                    Debug.Print doubleSource

                    Dim dateSource As Date
                    Dim datePointer As Long
                    dateSource = CDate(1.5)
                    datePointer = VarPtr(dateSource)
                    CopyMemory doubleDestination, ByVal datePointer, 8
                    Debug.Print doubleDestination

                    dateSource = CDate(2.5)
                    CopyMemory doubleDestination, ByVal datePointer, 8
                    Debug.Print doubleDestination

                    doubleDestination = 3
                    CopyMemory ByVal datePointer, doubleDestination, 8
                    Debug.Print CDbl(dateSource)

                    Dim currencySource As Currency
                    Dim currencyRaw As LongLong
                    Dim currencyPointer As Long
                    currencySource = CCur(1.5)
                    currencyPointer = VarPtr(currencySource)
                    CopyMemory currencyRaw, ByVal currencyPointer, 8
                    Debug.Print currencyRaw

                    currencySource = CCur(2.5)
                    CopyMemory currencyRaw, ByVal currencyPointer, 8
                    Debug.Print currencyRaw

                    currencyRaw = 30000
                    CopyMemory ByVal currencyPointer, currencyRaw, 8
                    Debug.Print currencySource

                    Dim longLongSource As LongLong
                    Dim longLongDestination As LongLong
                    Dim longLongPointer As Long
                    longLongSource = 72623859790382856
                    longLongPointer = VarPtr(longLongSource)
                    CopyMemory longLongDestination, ByVal longLongPointer, 8
                    Debug.Print longLongDestination

                    longLongSource = 123
                    CopyMemory longLongDestination, ByVal longLongPointer, 8
                    Debug.Print longLongDestination

                    longLongDestination = 84281096
                    CopyMemory ByVal longLongPointer, longLongDestination, 8
                    Debug.Print longLongSource

                    Dim longPtrSource As LongPtr
                    Dim longPtrDestination As LongPtr
                    Dim longPtrPointer As Long
                    longPtrSource = CLngPtr(16909060)
                    longPtrPointer = VarPtr(longPtrSource)
                    CopyMemory longPtrDestination, ByVal longPtrPointer, 4
                    Debug.Print CLng(longPtrDestination)

                    longPtrSource = CLngPtr(123)
                    CopyMemory longPtrDestination, ByVal longPtrPointer, 4
                    Debug.Print CLng(longPtrDestination)

                    longPtrDestination = CLngPtr(84281096)
                    CopyMemory ByVal longPtrPointer, longPtrDestination, 4
                    Debug.Print CLng(longPtrSource)

                    Dim uShortSource As UShort
                    Dim uShortDestination As UShort
                    Dim uShortPointer As Long
                    uShortSource = CUShort(50000)
                    uShortPointer = VarPtr(uShortSource)
                    CopyMemory uShortDestination, ByVal uShortPointer, 2
                    Debug.Print CLng(uShortDestination)

                    uShortSource = CUShort(123)
                    CopyMemory uShortDestination, ByVal uShortPointer, 2
                    Debug.Print CLng(uShortDestination)

                    uShortDestination = CUShort(40000)
                    CopyMemory ByVal uShortPointer, uShortDestination, 2
                    Debug.Print CLng(uShortSource)

                    Dim uIntegerSource As UInteger
                    Dim uIntegerDestination As UInteger
                    Dim uIntegerPointer As Long
                    uIntegerSource = CUInt(4000000000)
                    uIntegerPointer = VarPtr(uIntegerSource)
                    CopyMemory uIntegerDestination, ByVal uIntegerPointer, 4
                    Debug.Print uIntegerDestination

                    uIntegerSource = CUInt(123)
                    CopyMemory uIntegerDestination, ByVal uIntegerPointer, 4
                    Debug.Print uIntegerDestination

                    uIntegerDestination = CUInt(3000000000)
                    CopyMemory ByVal uIntegerPointer, uIntegerDestination, 4
                    Debug.Print uIntegerSource

                    Dim uLongSource As ULong
                    Dim uLongDestination As ULong
                    Dim uLongPointer As Long
                    uLongSource = CULng("18446744073709551614")
                    uLongPointer = VarPtr(uLongSource)
                    CopyMemory uLongDestination, ByVal uLongPointer, 8
                    Debug.Print uLongDestination

                    uLongSource = CULng(123)
                    CopyMemory uLongDestination, ByVal uLongPointer, 8
                    Debug.Print uLongDestination

                    uLongDestination = CULng("10000000000000000000")
                    CopyMemory ByVal uLongPointer, uLongDestination, 8
                    Debug.Print uLongSource
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("StoredVarPtr", Platform: ManagedPlatform.X86));
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

            var startInfo = new ProcessStartInfo(dotnetHost)
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(assemblyPath);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The x86 stored-VarPtr probe could not start.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "16909060", "123", "84281096", "1690", "123", "8428", "169", "123", "42", "-1", "0", "True", "1069547520", "1075838976", "3", "1.5", "2.5", "3", "1.5", "2.5", "3", "15000", "25000", "3", "72623859790382856", "123", "84281096", "16909060", "123", "84281096", "50000", "123", "40000", "4000000000", "123", "3000000000", "18446744073709551614", "123", "10000000000000000000" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                Thread.Sleep(200);
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(200);
            }
        }
    }
}
