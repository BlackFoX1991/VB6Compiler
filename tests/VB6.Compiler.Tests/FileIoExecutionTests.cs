namespace VB6.Compiler.Tests;

/// <summary>
/// End to end binary file I/O: the generated program writes a file, seeks inside it, and reads the
/// values back. The shapes come from the conformance corpus, which opens files with
/// <c>Open ... For Binary As #FileNum</c> and reads with <c>Get #FileNum, , value</c>.
/// </summary>
[TestClass]
public sealed class FileIoExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsBinaryFiles()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim path As String
                Dim written As Long
                Dim readBack As Long
                Dim flag As Byte

                path = "roundtrip.bin"
                written = 123456

                Open path For Binary As #1
                Put #1, 1, written
                Put #1, , flag
                Close #1

                Open path For Binary As #1
                Get #1, 1, readBack
                Close #1

                Debug.Print readBack
            End Sub
            """,
            "123456");
    }

    /// <summary>Positions are one-based, and an omitted position continues where the last one stopped.</summary>
    [TestMethod]
    public void EmitManagedApplication_UsesOneBasedPositionsAndContinuesWhenOmitted()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim first As Byte
                Dim second As Byte

                first = 10
                second = 20

                Open "bytes.bin" For Binary As #1
                Put #1, 1, first
                Put #1, 2, second
                Close #1

                first = 0
                second = 0

                Open "bytes.bin" For Binary As #1
                Seek #1, 1
                Get #1, , first
                Get #1, , second
                Close #1

                Debug.Print first
                Debug.Print second
            End Sub
            """,
            "10",
            "20");
    }

    /// <summary>
    /// The file functions, including <c>FreeFile</c> written without parentheses - which is how VB6
    /// calls a function that takes no arguments.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_UsesFreeFileLofAndEof()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim FileNum As Long
                Dim value As Long

                FileNum = FreeFile
                Debug.Print FileNum

                Open "counted.bin" For Binary As #FileNum
                value = 42
                Put #FileNum, 1, value
                Debug.Print LOF(FileNum)
                Debug.Print EOF(FileNum)
                Seek #FileNum, 1
                Debug.Print EOF(FileNum)
                Close #FileNum
            End Sub
            """,
            "1",
            "4",
            "True",
            "False");
    }

    [TestMethod]
    public void Analyze_ReportsTransfersThatHaveNoLayoutRuleYet()
    {
        var analysis = VBCompilation.Create("""
            Type Record
                Value As Long
            End Type

            Sub Main()
                Dim record As Record
                Open "a.bin" For Binary As #1
                Get #1, 1, record
                Close #1
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0058");
        StringAssert.Contains(diagnostic.Message, "UDT");
    }

    [TestMethod]
    public void Analyze_ReportsOpenModesOtherThanBinary()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Open "a.txt" For Output As #1
                Close #1
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsTrue(analysis.Diagnostics.Any(d => d.Code == "VB6S0057"));
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            expectedLines,
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
