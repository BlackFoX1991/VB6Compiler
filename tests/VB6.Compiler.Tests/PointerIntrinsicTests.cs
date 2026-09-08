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

                Sub ReplaceString(ByRef value As String)
                    value = "xy"
                End Sub

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

                    Dim stringSource As String
                    Dim stringPointer As Long
                    Dim character As Integer
                    stringSource = "abc"
                    stringPointer = StrPtr(stringSource)
                    CopyMemory character, ByVal stringPointer, 2
                    Debug.Print character

                    character = 90
                    CopyMemory ByVal stringPointer, character, 2
                    Debug.Print stringSource

                    ReplaceString stringSource
                    Debug.Print stringSource
                    stringPointer = StrPtr(stringSource)
                    CopyMemory character, ByVal stringPointer, 2
                    Debug.Print character
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
                new[] { "16909060", "123", "84281096", "1690", "123", "8428", "169", "123", "42", "-1", "0", "True", "1069547520", "1075838976", "3", "1.5", "2.5", "3", "1.5", "2.5", "3", "15000", "25000", "3", "72623859790382856", "123", "84281096", "16909060", "123", "84281096", "50000", "123", "40000", "4000000000", "123", "3000000000", "18446744073709551614", "123", "10000000000000000000", "97", "Zbc", "xy", "120" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsWhyAModuleVariableVarPtrCannotAnswer()
    {
        var output = VB6TestProgram.RunLines("""
            Private total As Long
            Private caption As String

            Sub Main()
                On Error Resume Next
                Dim pointer As Long
                total = 7
                pointer = VarPtr(total)
                Debug.Print Err.Number
                Err.Clear
                caption = "abc"
                pointer = StrPtr(caption)
                Debug.Print Err.Number
            End Sub
            """);

        // Die Zelle einer Modulvariablen ist genauso x86-gebunden wie die eines Locals.
        CollectionAssert.AreEqual(new[] { "5", "5" }, output);
    }

    [TestMethod]
    public void EmitManagedProject_LeavesAClassFieldWithoutAnAddressableCell()
    {
        // Ein Klassenfeld ist im Binder ebenfalls ein ModuleVariableSymbol, hat aber keinen
        // statischen Speicherplatz. Ohne die Unterscheidung im Lowerer bekaeme es eine Zelle,
        // fuer die es im Emitter kein Feld gibt -- und aus Fehler 5 wuerde ein Emitter-Defekt.
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerClassFieldVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "ClassFieldVarPtr.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ClassFieldVarPtr"
                Class=Counter; Counter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Counter.cls"), """
                Option Explicit

                Private total As Long

                Public Function PointerError() As Long
                    Dim pointer As Long
                    On Error Resume Next
                    total = 7
                    pointer = VarPtr(total)
                    PointerError = Err.Number
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim item As New Counter
                    Debug.Print item.PointerError()
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "5" },
                VB6TestProgram.RunProjectLines(projectPath));
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitX86Application_KeepsAStoredModuleVariableVarPtrSynchronizedWithNativeWrites()
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
            "VB6CompilerStoredGlobalVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "StoredGlobalVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Private total As Long
                Private small As Integer
                Private caption As String

                Sub Bump(ByRef value As Long)
                    value = value + 1
                End Sub

                Sub SetTotal(ByVal value As Long)
                    total = value
                End Sub

                Sub Main()
                    Dim destination As Long
                    Dim pointer As Long

                    total = 16909060
                    pointer = VarPtr(total)
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    total = 123
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    destination = 84281096
                    CopyMemory ByVal pointer, destination, 4
                    Debug.Print total

                    Bump total
                    Debug.Print total
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    SetTotal 4711
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    Dim shortDestination As Integer
                    Dim shortPointer As Long
                    small = 1690
                    shortPointer = VarPtr(small)
                    CopyMemory shortDestination, ByVal shortPointer, 2
                    Debug.Print shortDestination

                    shortDestination = 8428
                    CopyMemory ByVal shortPointer, shortDestination, 2
                    Debug.Print small

                    Dim character As Integer
                    Dim stringPointer As Long
                    caption = "abc"
                    stringPointer = StrPtr(caption)
                    CopyMemory character, ByVal stringPointer, 2
                    Debug.Print character

                    character = 90
                    CopyMemory ByVal stringPointer, character, 2
                    Debug.Print caption
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("StoredGlobalVarPtr", Platform: ManagedPlatform.X86));
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

            // Der Unterschied zum Local steht in den Zeilen vier bis sechs: Eine fremde Prozedur,
            // die ByRef schreibt oder gewoehnlich zuweist, wird ueber den gespeicherten Zeiger
            // sichtbar -- ein Local kann das gar nicht zeigen.
            CollectionAssert.AreEqual(
                new[] { "16909060", "123", "84281096", "84281097", "84281097", "4711", "1690", "8428", "97", "Zbc" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitX86Application_KeepsAStaticLocalVarPtrSynchronizedAcrossCalls()
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
            "VB6CompilerStaticLocalVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // Ein Static-Local ist Modulspeicher unter anderem Namen: Der Binder legt es als
            // ModuleVariableSymbol in einem eigenen Modul ab, die adressierende Prozedur steht
            // aber in ihrem. Genau diese Modulgrenze hat die Zelle zuerst unbrauchbar gemacht.
            var assemblyPath = Path.Combine(directory, "StaticLocalVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Sub Schritt()
                    Static gemerkt As Long
                    Dim destination As Long
                    Dim pointer As Long

                    gemerkt = gemerkt + 1
                    pointer = VarPtr(gemerkt)
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    destination = 100
                    CopyMemory ByVal pointer, destination, 4
                    Debug.Print gemerkt
                End Sub

                Sub Main()
                    Schritt
                    Schritt
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("StaticLocalVarPtr", Platform: ManagedPlatform.X86));
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

            // Der zweite Aufruf sieht die 101 des ersten: Der Speicherplatz ueberlebt die
            // Prozedurrueckkehr, und die Zelle tut es mit ihm.
            CollectionAssert.AreEqual(
                new[] { "1", "100", "101", "100" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsWhyAByRefParameterVarPtrCannotAnswer()
    {
        // Der Zielvertrag verlangt, dass ein Alias dieselbe Zelle sieht. Eine eigene Zelle im
        // Aufgerufenen waere eine zweite, entkoppelte Kopie -- der erklaerte Fehler 5 ist ehrlicher
        // als eine Adresse, die auf den falschen Speicher zeigt.
        var output = VB6TestProgram.RunLines("""
            Sub Zeige(ByRef wert As Long)
                On Error Resume Next
                Dim pointer As Long
                pointer = VarPtr(wert)
                Debug.Print Err.Number
            End Sub

            Sub Main()
                Dim wert As Long
                wert = 7
                Zeige wert
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "5" }, output);
    }

    [TestMethod]
    public void EmitX86Application_KeepsAByValParameterVarPtrOnItsOwnCopy()
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
            "VB6CompilerByValParameterVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "ByValParameterVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Sub Bump(ByRef value As Long)
                    value = value + 1
                End Sub

                Sub ZeigeLong(ByVal wert As Long)
                    Dim destination As Long
                    Dim pointer As Long

                    pointer = VarPtr(wert)
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    wert = 123
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination

                    destination = 84281096
                    CopyMemory ByVal pointer, destination, 4
                    Debug.Print wert

                    Bump wert
                    Debug.Print wert
                    CopyMemory destination, ByVal pointer, 4
                    Debug.Print destination
                End Sub

                Sub ZeigeString(ByVal text As String)
                    Dim character As Integer
                    Dim pointer As Long

                    pointer = StrPtr(text)
                    CopyMemory character, ByVal pointer, 2
                    Debug.Print character

                    character = 90
                    CopyMemory ByVal pointer, character, 2
                    Debug.Print text
                End Sub

                Sub Main()
                    Dim aufrufer As Long
                    aufrufer = 16909060
                    ZeigeLong aufrufer
                    Debug.Print aufrufer
                    ZeigeString "abc"
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("ByValParameterVarPtr", Platform: ManagedPlatform.X86));
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

            // Die vorletzte Zeile ist der eigentliche Punkt: Die Variable des Aufrufers steht
            // unveraendert auf 16909060. Die Zelle gehoert der Kopie, nicht dem Original.
            CollectionAssert.AreEqual(
                new[] { "16909060", "123", "84281096", "84281097", "84281097", "16909060", "97", "Zbc" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsWhyARecordWithItsOwnStorageCannotAnswer()
    {
        // Ein Arraymember besitzt Speicher neben dem Datensatz. Der Marshaller kennt fuer den
        // flachen Block kein Layout, das ein Declare wiederfaende -- also lieber die erklaerte 5.
        var output = VB6TestProgram.RunLines("""
            Private Type MitFeld
                Werte(3) As Long
            End Type

            Sub Main()
                On Error Resume Next
                Dim f As MitFeld
                Dim zeiger As Long
                zeiger = VarPtr(f)
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "5" }, output);
    }

    [TestMethod]
    public void EmitX86Application_KeepsARecordAndItsMembersInOneNativeBlock()
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
            "VB6CompilerRecordVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "RecordVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Private Type Innen
                    A As Long
                    B As Long
                End Type

                Private Type Punkt
                    X As Long
                    Y As Integer
                    Z As Double
                    Tief As Innen
                End Type

                Private globalPunkt As Punkt

                Sub Setze(ByRef ziel As Long)
                    ziel = 4711
                End Sub

                Sub Main()
                    Dim p As Punkt
                    Dim basis As Long
                    Dim z As Long
                    Dim gelesen As Double

                    p.X = 16909060
                    basis = VarPtr(p)

                    CopyMemory z, ByVal basis, 4
                    Debug.Print z

                    Debug.Print VarPtr(p.X) - basis
                    Debug.Print VarPtr(p.Y) - basis
                    Debug.Print VarPtr(p.Z) - basis
                    Debug.Print VarPtr(p.Tief) - basis
                    Debug.Print VarPtr(p.Tief.B) - basis

                    p.Z = 2.5
                    CopyMemory gelesen, ByVal (basis + 8), 8
                    Debug.Print gelesen

                    z = 99
                    CopyMemory ByVal basis, z, 4
                    Debug.Print p.X

                    Setze p.Tief.A
                    CopyMemory z, ByVal (basis + 16), 4
                    Debug.Print z

                    globalPunkt.Tief.B = 8
                    CopyMemory z, ByVal (VarPtr(globalPunkt) + 20), 4
                    Debug.Print z
                    Debug.Print VarPtr(globalPunkt.Tief.B) - VarPtr(globalPunkt)
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("RecordVarPtr", Platform: ManagedPlatform.X86));
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

            // Die Offsets sind das eigentliche Ergebnis: 0, 4, 8, 16, 20 ist genau das
            // Vier-Byte-Packing, das VB6 einem Type gibt -- zwei Byte Fuellung hinter dem
            // Integer, damit das Double auf acht liegt.
            CollectionAssert.AreEqual(
                new[]
                {
                    "16909060",
                    "0", "4", "8", "16", "20",
                    "2.5",
                    "99",
                    "4711",
                    "8", "20"
                },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsWhyARectangularArrayElementCannotAnswer()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim feld(2, 2) As Long
                Dim ganz(3) As Long
                Dim zeiger As Long
                zeiger = VarPtr(feld(1, 1))
                Debug.Print Err.Number
                Err.Clear
                zeiger = VarPtr(ganz)
                Debug.Print Err.Number
            End Sub
            """);

        // Mehrdimensional: die physische Reihenfolge ist nicht die eines SAFEARRAY. Das ganze
        // Array: VarPtr traefe in VB6 den Deskriptor, nicht die Daten -- ein eigener Vertrag.
        CollectionAssert.AreEqual(new[] { "5", "5" }, output);
    }

    [TestMethod]
    public void EmitX86Application_KeepsAnArrayElementPointerValidThroughEveryOtherWriter()
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
            "VB6CompilerArrayElementVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "ArrayElementVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Private globalFeld(3) As Long

                Sub SchreibeElement(ByRef feld() As Long)
                    feld(2) = 555
                End Sub

                Sub Main()
                    Dim fest(3) As Long
                    Dim ab5(5 To 8) As Long
                    Dim kurz(3) As Integer
                    Dim dyn() As Long
                    Dim z As Long
                    Dim basis As Long
                    Dim ballast As String
                    Dim i As Long

                    fest(0) = 16909060
                    basis = VarPtr(fest(0))

                    Debug.Print VarPtr(fest(1)) - basis
                    Debug.Print VarPtr(fest(3)) - basis

                    CopyMemory z, ByVal basis, 4
                    Debug.Print z

                    fest(0) = 4711
                    CopyMemory z, ByVal basis, 4
                    Debug.Print z

                    z = 99
                    CopyMemory ByVal basis, z, 4
                    Debug.Print fest(0)

                    For i = 1 To 2000
                        ballast = ballast & "x"
                    Next i
                    CopyMemory z, ByVal basis, 4
                    Debug.Print z

                    SchreibeElement fest
                    CopyMemory z, ByVal (basis + 8), 4
                    Debug.Print z

                    Debug.Print VarPtr(ab5(6)) - VarPtr(ab5(5))
                    Debug.Print VarPtr(kurz(1)) - VarPtr(kurz(0))

                    ReDim dyn(2)
                    Debug.Print VarPtr(dyn(1)) - VarPtr(dyn(0))
                    Debug.Print VarPtr(globalFeld(1)) - VarPtr(globalFeld(0))
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("ArrayElementVarPtr", Platform: ManagedPlatform.X86));
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

            // Zeile sechs und sieben sind der Punkt: Der Zeiger ueberlebt Speicherdruck, weil das
            // Array selbst unbeweglich geworden ist, und er sieht den Schreibzugriff einer fremden
            // Prozedur ueber die Arrayreferenz -- ein Abbild neben dem Array koennte das nicht.
            CollectionAssert.AreEqual(
                new[] { "4", "12", "16909060", "4711", "99", "99", "555", "4", "2", "4", "4" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitX86Application_KeepsAnImmediateDeclarePointerAndAStoredOneOnTheSameStorage()
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
            "VB6CompilerImmediateAndStoredVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // Ein Speicherplatz kann beide Formen tragen: den unmittelbaren ByVal VarPtr(x) eines
            // Declare und einen gespeicherten Zeiger. Der unmittelbaren Form fehlte das
            // Rueckschreiben in die Zelle, weil sie die Vorgabe-Argumentart traegt statt Address --
            // ihr Schreibzugriff ging dadurch verloren, sobald derselbe Platz eine Zelle hatte.
            var assemblyPath = Path.Combine(directory, "ImmediateAndStoredVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Private globalWert As Long

                Sub Main()
                    Dim lokal As Long
                    Dim gespeichert As Long
                    Dim z As Long

                    lokal = 1
                    gespeichert = VarPtr(lokal)
                    z = 99
                    CopyMemory ByVal VarPtr(lokal), z, 4
                    Debug.Print lokal
                    z = 0
                    CopyMemory z, ByVal gespeichert, 4
                    Debug.Print z

                    globalWert = 1
                    gespeichert = VarPtr(globalWert)
                    z = 77
                    CopyMemory ByVal VarPtr(globalWert), z, 4
                    Debug.Print globalWert
                    z = 0
                    CopyMemory z, ByVal gespeichert, 4
                    Debug.Print z
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("ImmediateAndStoredVarPtr", Platform: ManagedPlatform.X86));
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
                new[] { "99", "99", "77", "77" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitX86Application_AliasesByRefVarPtrToTheCallersNativeCell()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The ByRef VarPtr regression uses the Windows x86 host.");
            return;
        }

        var dotnetHost = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "dotnet",
            "dotnet.exe");
        if (!File.Exists(dotnetHost))
        {
            Assert.Inconclusive("The x86 .NET host required by the ByRef VarPtr contract is unavailable.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerByRefVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "ByRefVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Sub Beobachte(ByRef wert As Long, ByVal erwarteteAdresse As Long)
                    Dim zeiger As Long
                    Dim neu As Long
                    Dim gelesen As Long
                    Dim ballast As String
                    Dim i As Long

                    zeiger = VarPtr(wert)
                    Debug.Print (erwarteteAdresse = 0 Or zeiger = erwarteteAdresse)

                    neu = 99
                    CopyMemory ByVal zeiger, neu, 4
                    wert = wert + 1
                    For i = 1 To 2000
                        ballast = ballast & "x"
                    Next i
                    CopyMemory gelesen, ByVal zeiger, 4
                    Debug.Print gelesen
                End Sub

                Sub BeobachteBoolean(ByRef wert As Boolean, ByVal erwarteteAdresse As Long)
                    Dim zeiger As Long

                    zeiger = VarPtr(wert)
                    Debug.Print zeiger = erwarteteAdresse
                    Debug.Print wert
                    wert = False
                End Sub

                Sub BeobachteString(ByRef wert As String, ByVal erwarteteAdresse As Long)
                    Dim zeiger As Long

                    zeiger = VarPtr(wert)
                    Debug.Print zeiger = erwarteteAdresse
                    Debug.Print wert
                    wert = "aktualisiert"
                End Sub

                Sub PruefeWeitergabe(ByRef wert As Long, ByVal erwarteteAdresse As Long)
                    Debug.Print VarPtr(wert) = erwarteteAdresse
                    wert = wert + 1
                End Sub

                Sub LeiteWeiter(ByRef wert As Long, ByVal erwarteteAdresse As Long)
                    Dim zeiger As Long

                    zeiger = VarPtr(wert)
                    PruefeWeitergabe wert, erwarteteAdresse
                    Debug.Print zeiger = VarPtr(wert)
                End Sub

                Sub Main()
                    Dim mitZelle As Long
                    Dim ohneZelle As Long
                    Dim weiter As Long
                    Dim zeiger As Long
                    Dim wahr As Boolean
                    Dim text As String

                    mitZelle = 42
                    zeiger = VarPtr(mitZelle)
                    Beobachte mitZelle, zeiger
                    Debug.Print mitZelle

                    ohneZelle = 7
                    Beobachte ohneZelle, 0
                    Debug.Print ohneZelle

                    weiter = 5
                    zeiger = VarPtr(weiter)
                    LeiteWeiter weiter, zeiger
                    Debug.Print weiter

                    wahr = True
                    zeiger = VarPtr(wahr)
                    BeobachteBoolean wahr, zeiger
                    Debug.Print wahr

                    text = "vorher"
                    zeiger = VarPtr(text)
                    BeobachteString text, zeiger
                    Debug.Print text
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("ByRefVarPtr", Platform: ManagedPlatform.X86));
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
                ?? throw new InvalidOperationException("The x86 ByRef VarPtr probe could not start.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "True", "100", "100", "True", "100", "100", "True", "True", "6", "True", "True", "False", "True", "vorher", "aktualisiert" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitX86Application_LeavesAnAddressOfByRefProcedureAtError5()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The ByRef VarPtr regression uses the Windows x86 host.");
            return;
        }

        var dotnetHost = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "dotnet",
            "dotnet.exe");
        if (!File.Exists(dotnetHost))
        {
            Assert.Inconclusive("The x86 .NET host required by the ByRef VarPtr contract is unavailable.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerAddressOfByRefVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var assemblyPath = Path.Combine(directory, "AddressOfByRefVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Sub Beobachte(ByRef wert As Long)
                    Dim zeiger As Long
                    On Error Resume Next
                    zeiger = VarPtr(wert)
                    Debug.Print Err.Number
                    Debug.Print Err.Description
                End Sub

                Sub Main()
                    Dim callback As LongPtr
                    Dim wert As Long
                    callback = AddressOf Beobachte
                    Beobachte wert
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("AddressOfByRefVarPtr", Platform: ManagedPlatform.X86));
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
                ?? throw new InvalidOperationException("The x86 AddressOf ByRef VarPtr probe could not start.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[]
                {
                    "5",
                    "VarPtr is supported only as a ByVal As Any argument of a Declare, where the address is consumed immediately."
                },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitX86Project_GivesEveryInstanceItsOwnPrivateFieldCell()
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
            "VB6CompilerFieldVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "FieldVarPtr.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="FieldVarPtr"
                Class=Puffer; Puffer.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Puffer.cls"), """
                Option Explicit

                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Private wert As Long
                Private text As String
                Public offen As Long

                Private Sub Setze(ByRef ziel As Long)
                    ziel = 4711
                End Sub

                Public Function Zeiger() As Long
                    wert = 16909060
                    Zeiger = VarPtr(wert)
                End Function

                Public Function LiesUeberZeiger() As Long
                    Dim z As Long
                    CopyMemory z, ByVal VarPtr(wert), 4
                    LiesUeberZeiger = z
                End Function

                Public Sub SchreibeUeberZeiger(ByVal neu As Long)
                    CopyMemory ByVal VarPtr(wert), neu, 4
                End Sub

                Public Function Adresse() As Long
                    Adresse = VarPtr(wert)
                End Function

                Public Function LiesWert() As Long
                    LiesWert = wert
                End Function

                Public Sub SetzeUeberByRef()
                    Setze wert
                End Sub

                Public Function TextZeiger() As Long
                    text = "abc"
                    TextZeiger = StrPtr(text)
                End Function

                Public Function OffenerZeigerFehler() As Long
                    Dim z As Long
                    On Error Resume Next
                    z = VarPtr(offen)
                    OffenerZeigerFehler = Err.Number
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Sub Main()
                    Dim p As New Puffer
                    Dim q As New Puffer

                    Debug.Print (p.Zeiger() <> 0)
                    Debug.Print p.LiesUeberZeiger()

                    p.SchreibeUeberZeiger 99
                    Debug.Print p.LiesWert()

                    p.SetzeUeberByRef
                    Debug.Print p.LiesWert()
                    Debug.Print p.LiesUeberZeiger()

                    Dim z As Long
                    z = 12345
                    CopyMemory ByVal p.Adresse(), z, 4
                    Debug.Print p.LiesWert()
                    Debug.Print p.LiesUeberZeiger()

                    Debug.Print (p.Zeiger() <> q.Zeiger())
                    Debug.Print (p.TextZeiger() <> 0)
                    Debug.Print p.OffenerZeigerFehler()
                End Sub
                """);

            var assemblyPath = Path.Combine(directory, "FieldVarPtr.dll");
            var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("FieldVarPtr", Platform: ManagedPlatform.X86));
            Assert.IsTrue(
                result.Success,
                string.Join(Environment.NewLine, result.Lowering.Analysis.Diagnostics));

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

            // Die letzte Zeile ist die Grenze: Ein Public-Feld bleibt bei Fehler 5, weil die spaete
            // Bindung es per Reflection direkt aus dem CLR-Feld liest und eine Zelle daneben dort
            // unsichtbar waere. Die vorletzte: jede Instanz hat ihre eigene Zelle.
            CollectionAssert.AreEqual(
                new[] { "True", "16909060", "99", "4711", "4711", "12345", "12345", "True", "True", "5" },
                VB6TestProgram.SplitLines(standardOutput),
                standardOutput);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitX86Application_SeparatesTheStringVariableFromItsBstr()
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
            "VB6CompilerStringVarPtrTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            // Ein String-Speicherplatz ist zwei Dinge, und jedes hat seine Intrinsic: Die Variable
            // haelt einen Zeiger auf die BSTR. Die dokumentierte Beziehung ist exakt und braucht
            // dafuer kein VB6-Orakel -- StrPtr(s) ist der Long, der an VarPtr(s) steht.
            var assemblyPath = Path.Combine(directory, "StringVarPtr.dll");
            var result = VBCompilation.Create("""
                Private Declare Sub CopyMemory Lib "kernel32" Alias "RtlMoveMemory" (Destination As Any, Source As Any, ByVal Length As Long)

                Private globalText As String

                Sub Main()
                    Dim s As String
                    Dim variablenZeiger As Long
                    Dim inhaltsZeiger As Long
                    Dim gelesen As Long
                    Dim zeichen As Integer

                    s = "abc"
                    variablenZeiger = VarPtr(s)
                    inhaltsZeiger = StrPtr(s)
                    Debug.Print ((variablenZeiger <> 0) And (inhaltsZeiger <> 0))
                    Debug.Print (variablenZeiger <> inhaltsZeiger)

                    CopyMemory gelesen, ByVal variablenZeiger, 4
                    Debug.Print (gelesen = inhaltsZeiger)

                    s = "wxyz"
                    CopyMemory gelesen, ByVal variablenZeiger, 4
                    Debug.Print (gelesen = StrPtr(s))
                    CopyMemory zeichen, ByVal gelesen, 2
                    Debug.Print zeichen

                    globalText = "de"
                    CopyMemory gelesen, ByVal VarPtr(globalText), 4
                    Debug.Print (gelesen = StrPtr(globalText))
                End Sub
                """, "Module1.bas").EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions("StringVarPtr", Platform: ManagedPlatform.X86));
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

            // 119 ist "w": Nach der Neuzuweisung zeigt die Variable auf eine andere BSTR, und die
            // letzte Zeile prueft, dass auch das unmittelbare ByVal VarPtr(x) eines Declare dieselbe
            // Adresse nennt -- die verwaltete Adresse des Speicherplatzes waere dort ein
            // Objektzeiger gewesen.
            CollectionAssert.AreEqual(
                new[] { "True", "True", "True", "True", "119", "True" },
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
