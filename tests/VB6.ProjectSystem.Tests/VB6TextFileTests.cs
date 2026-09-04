using System.Text;
using VB6.ProjectSystem;

namespace VB6.ProjectSystem.Tests;

/// <summary>
/// How a VB6 file is decoded. VB6 saved Western-European projects in the Windows ANSI code page,
/// and a <c>.frm</c> written that way carries a caption as single bytes — reading it as UTF-8 turns
/// "Grüße" into replacement characters or throws. Newer tooling writes the same files with a BOM,
/// so both have to work, and the byte order mark decides which.
/// </summary>
[TestClass]
public sealed class VB6TextFileTests
{
    [TestMethod]
    public void ReadAllText_DecodesWindowsAnsiWithoutAByteOrderMark()
    {
        // Die Bytes stehen ausdrucksweise da, statt über Encoding.GetEncoding(1252) erzeugt zu
        // werden: der Test soll nicht denselben Anbieter voraussetzen, den er prüft.
        var path = WriteBytes(
        [
            .. "Caption = \"Gr"u8,
            0xFC, // ü
            0xDF, // ß
            .. "e\"\r\n"u8,
        ]);

        try
        {
            Assert.AreEqual("Caption = \"Grüße\"\r\n", VB6TextFile.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadAllText_PrefersUtf8WhenTheBytesAreValidUtf8()
    {
        // Ohne BOM ist UTF-8 die erste Lesart; erst wenn die Bytes darin nicht aufgehen, gilt 1252.
        var path = WriteBytes(new UTF8Encoding(false).GetBytes("Caption = \"Grüße\"\r\n"));

        try
        {
            Assert.AreEqual("Caption = \"Grüße\"\r\n", VB6TextFile.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadAllText_StripsEveryDocumentedByteOrderMark()
    {
        const string content = "Caption = \"Grüße\"\r\n";
        var cases = new (string Name, byte[] Bytes)[]
        {
            ("UTF-8", [.. new byte[] { 0xEF, 0xBB, 0xBF }, .. new UTF8Encoding(false).GetBytes(content)]),
            ("UTF-16 LE", [.. new byte[] { 0xFF, 0xFE }, .. Encoding.Unicode.GetBytes(content)]),
            ("UTF-16 BE", [.. new byte[] { 0xFE, 0xFF }, .. Encoding.BigEndianUnicode.GetBytes(content)]),
            ("UTF-32 LE", [.. new byte[] { 0xFF, 0xFE, 0x00, 0x00 }, .. Encoding.UTF32.GetBytes(content)]),
            ("UTF-32 BE",
                [
                    .. new byte[] { 0x00, 0x00, 0xFE, 0xFF },
                    .. new UTF32Encoding(bigEndian: true, byteOrderMark: false).GetBytes(content)
                ]),
        };

        foreach (var (name, bytes) in cases)
        {
            var path = WriteBytes(bytes);
            try
            {
                Assert.AreEqual(content, VB6TextFile.ReadAllText(path), name);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }

    private static string WriteBytes(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), "VB6TextFile-" + Guid.NewGuid().ToString("N") + ".frm");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
