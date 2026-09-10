using System.Globalization;
using System.Text;
using VB6.Runtime;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FileStringIoExecutionTests
{
    /// <summary>
    /// The VB6 rule a round trip cannot show: in Binary mode the target's own length is the read
    /// request.
    ///
    /// This case used to read into an unset variable and expect the whole string back. It passed
    /// because <c>Put</c> wrote a length descriptor that <c>Get</c> then read -- a pair confirming
    /// itself. The original does neither: measured on 2026-09-10, reading a six-byte file into a
    /// three-character variable yields three characters, and reading it into an empty one yields
    /// nothing at all. Both are asserted here, because the empty case is the one that silently
    /// looked right before.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_SizesABinaryStringReadByTheTargetVariable()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim path As String
                Dim written As String
                Dim sized As String
                Dim unset As String

                path = "string.bin"
                written = "ABCDEF"
                Open path For Binary As #1
                Put #1, 1, written
                Close #1

                sized = "xyz"
                Open path For Binary As #1
                Get #1, 1, sized
                Close #1
                Debug.Print sized

                Open path For Binary As #1
                Get #1, 1, unset
                Close #1
                Debug.Print "[" & unset & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "ABC", "[]" }, VB6TestProgram.SplitLines(output), output);
    }

    /// <summary>
    /// Random mode keeps the descriptor, so a read there does not depend on the target's length.
    /// The same program shape as above with one word changed gives a different answer, which is
    /// exactly the distinction the previous single round-trip case could not make.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_ReadsARandomStringFromItsDescriptor()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim written As String
                Dim readBack As String

                written = "Grüße"
                Open "string.rnd" For Random As #1 Len = 64
                Put #1, 1, written
                Close #1

                Open "string.rnd" For Random As #1 Len = 64
                Get #1, 1, readBack
                Close #1
                Debug.Print readBack
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Grüße" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesTheSelectedProfileToSequentialTextTransfers()
    {
        var compilation = VBCompilation.Create(
            """
            Sub Main()
                Dim firstByte As Byte
                Dim printed As String
                Dim written As String

                Open "profile-text.txt" For Output As #1
                Print #1, "ä"
                Write #1, "ö"
                Close #1

                Open "profile-text.txt" For Binary As #1
                Get #1, 1, firstByte
                Close #1

                Open "profile-text.txt" For Input As #1
                Line Input #1, printed
                Input #1, written
                Close #1

                Debug.Print firstByte
                Debug.Print printed
                Debug.Print written
            End Sub
            """,
            "Module1.bas",
            new VBCompilationOptions
            {
                CompatibilityProfile = VBCompatibilityProfile.VB6Sp6
            });

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Encoding.GetEncoding(
            0,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
        var expectedFirstByte = ansi.GetBytes("ä")[0].ToString(CultureInfo.InvariantCulture);

        CollectionAssert.AreEqual(
            new[] { expectedFirstByte, "ä", "ö" },
            VB6TestProgram.SplitLines(VB6TestProgram.Run(compilation)));
    }

    [TestMethod]
    public void EmitManagedApplication_ReadsCharactersThroughTheInputFunction()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim f As Integer
                Dim s As String
                Dim d As Variant

                f = FreeFile
                Open "zeichen.txt" For Output As #f
                Print #f, "abcdef"
                Close #f

                f = FreeFile
                Open "zeichen.txt" For Input As #f
                s = Input(3, #f)
                Debug.Print s
                s = Input(2, f)
                Debug.Print s
                Close #f

                f = FreeFile
                Open "zeichen.txt" For Input As #f
                Debug.Print LOF(#f) & " " & EOF(#f) & " " & Seek(#f)
                Close #f

                d = #1/2/2020#
                Debug.Print VarType(d) & " " & Year(d)

                Kill "zeichen.txt"
            End Sub
            """);

        // Die Funktionsform Input(n, #f) wurde vorher nicht geparst -- VB6P0001 auf dem
        // HashToken -- obwohl Intrinsic und Runtime laengst vorhanden waren. VB6 erlaubt den
        // Kanalmarker in jeder Argumentliste, auch bei LOF, EOF und Seek. Das Datumsliteral
        // bleibt davon unberuehrt: der Lexer trennt es schon vom blanken Rautenzeichen.
        CollectionAssert.AreEqual(
            new[] { "abc", "de", "8 False 1", "7 2020" },
            output);
    }
}
