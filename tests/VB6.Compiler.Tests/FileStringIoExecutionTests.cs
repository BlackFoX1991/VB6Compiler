using System.Globalization;
using System.Text;
using VB6.Runtime;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FileStringIoExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsVariableLengthStrings()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim path As String
                Dim written As String
                Dim readBack As String

                path = "string.bin"
                written = "Grüße"
                Open path For Binary As #1
                Put #1, 1, written
                Close #1

                Open path For Binary As #1
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
}
