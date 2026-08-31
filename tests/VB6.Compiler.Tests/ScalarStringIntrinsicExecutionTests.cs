using System.Globalization;
using System.Text;
using VB6.Runtime;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ScalarStringIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesSearchReplacementAndConversionIntrinsics()
    {
        var output = VB6TestProgram.Run("""
            Option Compare Text

            Sub Main()
                Debug.Print InStr("abc", "B")
                Debug.Print InStr(1, "abc", "B", vbTextCompare)
                Debug.Print InStrRev("abca", "A", -1, vbTextCompare)
                Debug.Print StrComp("abc", "ABC", vbBinaryCompare)
                Debug.Print StrComp("abc", "ABC", vbTextCompare)
                Debug.Print StrComp("b", "a")
                Debug.Print StrComp("abc", "ABC")
                Debug.Print Replace("a-b-b", "B", "x")
                Debug.Print StrConv("aBc", vbUpperCase)
                Debug.Print Int(-1.2)
                Debug.Print UBound(Split("a,b,c", ","))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2", "2", "4", "1", "0", "1", "0", "a-x-x", "ABC", "-2", "2" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesSelectedProfileToStrConv()
    {
        var compilation = VBCompilation.Create(
            "Sub Main()\n    Debug.Print StrConv(\"aBc\", vbUpperCase)\nEnd Sub\n",
            "Module1.bas",
            new VBCompilationOptions
            {
                CompatibilityProfile = VBCompatibilityProfile.VB6Sp6
            });

        var output = VB6TestProgram.SplitLines(VB6TestProgram.Run(compilation));

        CollectionAssert.AreEqual(new[] { "ABC" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PassesSelectedProfileToLenBAscAndChr()
    {
        var compilation = VBCompilation.Create(
            "Sub Main()\n    Debug.Print LenB(\"ä\")\n    Debug.Print Asc(\"ä\")\n    Debug.Print Chr(65)\nEnd Sub\n",
            "Module1.bas",
            new VBCompilationOptions
            {
                CompatibilityProfile = VBCompatibilityProfile.VB6Sp6
            });

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(
            0,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
        var expectedLength = encoding.GetByteCount("ä").ToString(CultureInfo.InvariantCulture);
        var expectedAsc = encoding.GetBytes("ä") is { Length: 1 } bytes
            ? bytes[0].ToString(CultureInfo.InvariantCulture)
            : null;

        var lines = VB6TestProgram.SplitLines(VB6TestProgram.Run(compilation));
        Assert.AreEqual(expectedLength, lines[0]);
        if (expectedAsc is not null)
        {
            Assert.AreEqual(expectedAsc, lines[1]);
        }

        Assert.AreEqual("A", lines[2]);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesByteStringIntrinsicsWithProfileAndCompareOption()
    {
        var compilation = VBCompilation.Create(
            """
            Option Compare Text

            Sub Main()
                Debug.Print LeftB("abcdef", 3)
                Debug.Print RightB("abcdef", 3)
                Debug.Print MidB("abcdef", 2, 3)
                Debug.Print InStrB("XXpXXp", "P")
                Debug.Print InStrB(1, "XXpXXp", "P", vbBinaryCompare)
            End Sub
            """,
            "Module1.bas",
            new VBCompilationOptions
            {
                CompatibilityProfile = VBCompatibilityProfile.VB6Sp6
            });

        CollectionAssert.AreEqual(
            new[] { "abc", "def", "bcd", "3", "0" },
            VB6TestProgram.SplitLines(VB6TestProgram.Run(compilation)));
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesUnicodeStringIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print AscW(ChrW(&H20AC))
                Debug.Print AscW("A")
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "8364", "65" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesWindows1252StringIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Asc("€")
                Debug.Print Asc("ä")
                Debug.Print Asc(Chr(128))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "128", "228", "128" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesBoundsAndElementsOfAnArrayHeldInVariant()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim values As Variant
                values = Split("a,b,c", ",")
                Debug.Print LBound(values)
                Debug.Print UBound(values)
                Debug.Print values(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0", "2", "b" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesFormatStringAndNumericMasks()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print Format$(5459.4, "##,##0.00")
                Debug.Print Format$(5, "0.00%")
                Debug.Print Format$("HELLO", "<")
                Debug.Print Format$("hello", ">")
                Debug.Print Format$("AB", "@@@")
                Debug.Print Format$("AB", "!@@@")
                Debug.Print Format$("AB", "&&&")
                Debug.Print Format$("", "@@;empty")
                Debug.Print Format$(True, "Yes/No")
                Debug.Print Format$(0, "On/Off")
                Debug.Print Format$(CDate(43832), "yyyy-mm-dd")
                Debug.Print Format$(CDate(43832), "yy ddddd")
                Debug.Print Format$(CDate(0.5), "hh:nn:ss")
                Debug.Print Format$(CDate(0.5), "ttttt")
                Debug.Print Format$(CDate(43835), "w ww q y", vbMonday, vbFirstFourDays)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "5,459.40", "500.00%", "hello", "HELLO", "AB", "AB", "AB", "empty",
                "Yes", "Off", "2020-01-02", "20 2020-01-02", "12:00:00", "12:00:00", "7 1 1 5"
            },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesExtendedStringFormattingIntrinsics()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print StrReverse("stressed")
                Debug.Print FormatNumber(1234.5)
                Debug.Print FormatNumber(0.5, 2, vbFalse, vbFalse, vbFalse)
                Debug.Print FormatCurrency(12.5, 1)
                Debug.Print FormatPercent(0.125, 1)
                Debug.Print FormatDateTime(CDate(43832), vbShortDate)
                Debug.Print "[" & Partition(17, 0, 99, 10) & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "desserts", "1,234.50", ".50", "$12.5", "12.5%", "2020-01-02", "[10:19]" },
            VB6TestProgram.SplitLines(output),
            output);
    }
}
