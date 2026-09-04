using System.Text;

namespace VB6.Compiler.Tests;

/// <summary>
/// The Win32 resource file a project names with <c>ResFile32=</c>. VB6 links its contents into the
/// executable, which is what makes <c>LoadResString</c> work in a deployed program without shipping
/// the <c>.res</c> beside it; the emitter embeds the same bytes as a managed resource.
///
/// The addressing is the part that is easy to get wrong: <c>LoadResString(id)</c> does not name a
/// string resource. Win32 stores strings in blocks of sixteen, the block id is <c>id \ 16 + 1</c>
/// and the position inside it is <c>id Mod 16</c>. Reading a block as one string returns the whole
/// table instead of one entry.
/// </summary>
[TestClass]
public sealed class ResourceFileExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ReadsStringsFromTheProjectResourceFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ResourceFile",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(
                Path.Combine(directory, "Probe.res"),
                BuildStringTable(blockId: 1, "Hallo", "Welt"));
            File.WriteAllText(Path.Combine(directory, "Probe.vbp"), """
                Type=Exe
                Startup="Sub Main"
                Name="Probe"
                ResFile32="Probe.res"
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Debug.Print LoadResString(0)
                    Debug.Print LoadResString(1)

                    ' Eine Kennung, die nicht in der Datei steht, ist in VB6 Fehler 326 -- keine
                    ' leere Zeichenkette, die wie ein Ergebnis aussieht.
                    On Error Resume Next
                    Err.Clear
                    Debug.Print LoadResString(500)
                    Debug.Print Err.Number
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Hallo", "Welt", "326" },
                VB6TestProgram.SplitLines(
                    VB6TestProgram.RunProject(Path.Combine(directory, "Probe.vbp"))));
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
    public void EmitManagedApplication_ReportsAMissingProjectResourceFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ResourceMissing",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Probe.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Probe"
                ResFile32="FehltGanz.res"
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Debug.Print 1
                End Sub
                """);

            // Gemeldet, nicht stillschweigend weggelassen: sonst antwortete jedes LoadResString im
            // Programm mit 326 und zeigte damit auf den Aufruf statt auf die Projektzeile.
            var error = Assert.ThrowsException<ManagedArtifactException>(() =>
                VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                    Path.Combine(directory, "out", "Probe.exe")));
            StringAssert.Contains(error.Message, "FehltGanz.res", error.Message);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// A minimal Win32 resource file: the leading placeholder entry and one RT_STRING block of
    /// sixteen length-prefixed UTF-16 strings.
    /// </summary>
    private static byte[] BuildStringTable(int blockId, params string[] values)
    {
        var block = new MemoryStream();
        for (var index = 0; index < 16; index++)
        {
            var text = index < values.Length ? values[index] : string.Empty;
            block.Write(BitConverter.GetBytes((ushort)text.Length));
            block.Write(Encoding.Unicode.GetBytes(text));
        }

        var file = new MemoryStream();
        WriteEntry(file, type: 0, name: 0, []);
        WriteEntry(file, type: 6, name: blockId, block.ToArray());
        return file.ToArray();
    }

    private static void WriteEntry(Stream target, int type, int name, byte[] data)
    {
        var header = new MemoryStream();
        header.Write(BitConverter.GetBytes(data.Length));
        header.Write(BitConverter.GetBytes(8 + 8 + 16));
        WriteOrdinal(header, type);
        WriteOrdinal(header, name);
        header.Write(new byte[16]); // DataVersion, MemoryFlags, LanguageId, Version, Characteristics

        WriteAligned(target, header.ToArray());
        WriteAligned(target, data);
    }

    private static void WriteOrdinal(Stream target, int value)
    {
        target.Write(BitConverter.GetBytes((ushort)0xFFFF));
        target.Write(BitConverter.GetBytes((ushort)value));
    }

    private static void WriteAligned(Stream target, byte[] bytes)
    {
        target.Write(bytes);
        var padding = (4 - (bytes.Length % 4)) % 4;
        if (padding > 0)
        {
            target.Write(new byte[padding]);
        }
    }
}
