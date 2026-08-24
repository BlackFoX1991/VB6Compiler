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

    [TestMethod]
    public void EmitManagedApplication_UsesTextOpenModesAndPrint()
    {
        Run("""
            Sub Main()
                Dim first As String
                Dim second As String

                Open "text.txt" For Output As #1
                Print #1, "hello"
                Close #1

                Open "text.txt" For Append As #1
                Print #1, "world"
                Close #1

                Open "text.txt" For Input As #1
                Line Input #1, first
                Line Input #1, second
                Close #1

                Debug.Print first
                Debug.Print second
            End Sub
            """,
            "hello",
            "world");
    }

    [TestMethod]
    public void EmitManagedApplication_ReadsInputFieldsIntoStringTargets()
    {
        Run("""
            Sub Main()
                Dim first As String
                Dim second As String

                Open "fields.txt" For Output As #1
                Print #1, "alpha,beta"
                Close #1

                Open "fields.txt" For Input As #1
                Input #1, first, second
                Close #1

                Debug.Print first
                Debug.Print second
            End Sub
            """,
            "alpha",
            "beta");
    }

    [TestMethod]
    public void EmitManagedApplication_ReadsInputFieldsIntoScalarTargets()
    {
        Run("""
            Sub Main()
                Dim count As Long
                Dim ratio As Double
                Dim enabled As Boolean
                Dim amount As Currency

                Open "scalars.txt" For Output As #1
                Print #1, 42
                Print #1, 1.25
                Print #1, True
                Print #1, 12.5
                Close #1

                Open "scalars.txt" For Input As #1
                Input #1, count, ratio, enabled, amount
                Close #1

                Debug.Print count
                Debug.Print ratio
                Debug.Print enabled
                Debug.Print amount
            End Sub
            """,
            "42",
            "1.25",
            "True",
            "12.5");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsScalarUdtRecords()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Type Header
                Code As Integer
            End Type

            Type Record
                Number As Long
                Meta As Header
            End Type

            Public Sub Main()
                Dim written As Record
                Dim readBack As Record

                written.Number = 123456
                written.Meta.Code = 321

                Open "record.bin" For Binary As #1
                Put #1, 1, written
                Close #1

                Open "record.bin" For Binary As #1
                Get #1, 1, readBack
                Debug.Print LOF(1)
                Close #1

                Debug.Print readBack.Number
                Debug.Print readBack.Meta.Code
            End Sub
            """,
            "6",
            "123456",
            "321");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsRandomScalarRecords()
    {
        Run("""
            Sub Main()
                Dim first As Long
                Dim second As Long

                Open "random.bin" For Random As #1 Len = 8
                first = 10
                second = 20
                Put #1, 1, first
                Put #1, , second
                Debug.Print LOF(1)
                Debug.Print Seek(1)
                Close #1

                Open "random.bin" For Random As #1 Len = 8
                Get #1, 1, first
                Get #1, , second
                Debug.Print first
                Debug.Print second
                Debug.Print Seek(1)
                Close #1
            End Sub
            """,
            "16",
            "3",
            "10",
            "20",
            "3");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsRandomScalarUdtRecords()
    {
        Run("""
            Type Header
                Code As Integer
            End Type

            Type Record
                Number As Long
                Meta As Header
            End Type

            Sub Main()
                Dim written As Record
                Dim readBack As Record

                written.Number = 123456
                written.Meta.Code = 321

                Open "random-record.bin" For Random As #1 Len = 6
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "random-record.bin" For Random As #1 Len = 6
                Get #1, 1, readBack
                Close #1

                Debug.Print readBack.Number
                Debug.Print readBack.Meta.Code
            End Sub
            """,
            "6",
            "123456",
            "321");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsRandomUdtArrayMembers()
    {
        Run("""
            Type Child
                Value As Long
            End Type

            Type Record
                Values(0 To 1, 1 To 2) As Integer
                Children(1 To 2) As Child
            End Type

            Sub Main()
                Dim written As Record
                Dim readBack As Record

                written.Values(0, 1) = 10
                written.Values(1, 2) = 40
                written.Children(1).Value = 100
                written.Children(2).Value = 200

                Open "random-array-record.bin" For Random As #1 Len = 16
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "random-array-record.bin" For Random As #1 Len = 16
                Get #1, 1, readBack
                Close #1

                Debug.Print readBack.Values(0, 1)
                Debug.Print readBack.Values(1, 2)
                Debug.Print readBack.Children(1).Value
                Debug.Print readBack.Children(2).Value
            End Sub
            """,
            "16",
            "10",
            "40",
            "100",
            "200");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsDynamicUdtArrayMembers()
    {
        Run("""
            Type Child
                Value As Long
            End Type

            Type Record
                Children() As Child
            End Type

            Sub Main()
                Dim written As Record
                Dim readBack As Record

                ReDim written.Children(1 To 2)
                written.Children(1).Value = 100
                written.Children(2).Value = 200

                Open "dynamic-array-record.bin" For Binary As #1
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "dynamic-array-record.bin" For Binary As #1
                Get #1, 1, readBack
                Close #1

                Debug.Print LBound(readBack.Children)
                Debug.Print UBound(readBack.Children)
                Debug.Print readBack.Children(1).Value
                Debug.Print readBack.Children(2).Value
            End Sub
            """,
            "18",
            "1",
            "2",
            "100",
            "200");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsUnallocatedDynamicUdtArrayMembers()
    {
        Run("""
            Type Record
                Values() As Long
            End Type

            Sub Main()
                Dim written As Record
                Dim readBack As Record

                Open "empty-dynamic-array-record.bin" For Binary As #1
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "empty-dynamic-array-record.bin" For Binary As #1
                Get #1, 1, readBack
                Close #1

                Open "empty-dynamic-array-record.bin" For Binary As #1
                Put #1, 1, readBack
                Debug.Print LOF(1)
                Close #1
            End Sub
            """,
            "2",
            "2");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsStandaloneFixedUdtArraysWithoutDescriptor()
    {
        Run("""
            Type Child
                Value As Long
            End Type

            Sub Main()
                Dim written(1 To 2) As Child
                Dim readBack(1 To 2) As Child

                written(1).Value = 100
                written(2).Value = 200

                Open "standalone-fixed-array.bin" For Binary As #1
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "standalone-fixed-array.bin" For Binary As #1
                Get #1, 1, readBack
                Close #1

                Debug.Print readBack(1).Value
                Debug.Print readBack(2).Value
            End Sub
            """,
            "8",
            "100",
            "200");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsStandaloneDynamicUdtArraysWithoutDescriptor()
    {
        Run("""
            Type Child
                Value As Long
            End Type

            Sub Main()
                Dim written() As Child
                Dim readBack() As Child

                ReDim written(1 To 2)
                ReDim readBack(1 To 2)
                written(1).Value = 300
                written(2).Value = 400

                Open "standalone-dynamic-array.bin" For Binary As #1
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "standalone-dynamic-array.bin" For Binary As #1
                Get #1, 1, readBack
                Close #1

                Debug.Print LBound(readBack)
                Debug.Print UBound(readBack)
                Debug.Print readBack(1).Value
                Debug.Print readBack(2).Value
            End Sub
            """,
            "8",
            "1",
            "2",
            "300",
            "400");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsVariableStringUdtFieldsWithDescriptor()
    {
        Run("""
            Type Record
                Code As Long
                Text As String
            End Type

            Sub Main()
                Dim written As Record
                Dim readBack As Record

                written.Code = 42
                written.Text = "Hi"

                Open "variable-string-record.bin" For Binary As #1
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "variable-string-record.bin" For Binary As #1
                Get #1, 1, readBack
                Close #1

                Debug.Print readBack.Code
                Debug.Print readBack.Text
            End Sub
            """,
            "10",
            "42",
            "Hi");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsDateUdtFieldsAsOleDoubles()
    {
        Run("""
            Type Record
                When As Date
            End Type

            Sub Main()
                Dim written As Record
                Dim readBack As Record

                written.When = 43832

                Open "date-record.bin" For Binary As #1
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "date-record.bin" For Binary As #1
                Get #1, 1, readBack
                Close #1

                Debug.Print CDbl(readBack.When)
            End Sub
            """,
            "8",
            "43832");
    }

    [TestMethod]
    public void EmitManagedApplication_ConvertsInputDateFieldsToOleDoubles()
    {
        Run("""
            Sub Main()
                Dim value As Date

                Open "date-input.txt" For Output As #1
                Print #1, "2020-01-02"
                Close #1

                Open "date-input.txt" For Input As #1
                Input #1, value
                Close #1

                Debug.Print CDbl(value)
            End Sub
            """,
            "43832");
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesCDateConversion()
    {
        Run("""
            Sub Main()
                Debug.Print CDbl(CDate("2020-01-02"))
                Debug.Print CDbl(CDate(43832))
            End Sub
            """,
            "43832",
            "43832");
    }

    [TestMethod]
    public void EmitManagedApplication_WritesAndReadsRandomFixedStringUdtRecords()
    {
        Run("""
            Type Record
                Code As Integer
                Name As String * 5
                Names(1 To 2) As String * 3
            End Type

            Sub Main()
                Dim written As Record
                Dim readBack As Record

                written.Code = 42
                written.Name = "Hi"
                written.Names(1) = "X"
                written.Names(2) = "YZ"

                Open "random-fixed-string-record.bin" For Random As #1 Len = 13
                Put #1, 1, written
                Debug.Print LOF(1)
                Close #1

                Open "random-fixed-string-record.bin" For Random As #1 Len = 13
                Get #1, 1, readBack
                Close #1

                Debug.Print readBack.Code
                Debug.Print "[" & readBack.Name & "]"
                Debug.Print "[" & readBack.Names(1) & "]"
                Debug.Print "[" & readBack.Names(2) & "]"
            End Sub
            """,
            "13",
            "42",
            "[Hi   ]",
            "[X  ]",
            "[YZ ]");
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
    public void EmitManagedApplication_UsesFilesystemPathIntrinsics()
    {
        Run("""
            Public Sub Main()
                Dim original As String

                original = CurDir()
                Open "source.txt" For Output As #1
                Print #1, "hello"
                Close #1

                FileCopy "source.txt", "copy.txt"
                Debug.Print FileLen("copy.txt")
                Debug.Print IsDate(FileDateTime("copy.txt"))

                MkDir "nested"
                Debug.Print (GetAttr("nested") And 16) = 16
                ChDir "nested"
                Debug.Print Len(CurDir()) > Len(original)
                ChDir ".."

                SetAttr "copy.txt", 1
                Debug.Print (GetAttr("copy.txt") And 1) = 1
                SetAttr "copy.txt", 0
                RmDir "nested"
                Kill "source.txt"
                Kill "copy.txt"
            End Sub
            """,
            "7",
            "True",
            "True",
            "True",
            "True");
    }

    [TestMethod]
    public void Analyze_ReportsTransfersThatHaveNoLayoutRuleYet()
    {
        var analysis = VBCompilation.Create("""
            Type Record
                Value As Variant
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
    public void EmitManagedApplication_UsesTheDefaultRandomRecordLength()
    {
        Run("""
            Sub Main()
                Dim value As Byte

                Open "a.dat" For Random As #1
                Put #1, 1, value
                Debug.Print LOF(1)
                Close #1
            End Sub
            """,
            "128");
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
