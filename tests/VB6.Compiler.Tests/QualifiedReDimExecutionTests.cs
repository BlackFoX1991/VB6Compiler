namespace VB6.Compiler.Tests;

/// <summary>
/// <c>ReDim Section(0).Bytes(0 To 4)</c> and the dynamic array member behind it. The shape comes
/// from the conformance corpus, where it was the first error in four modules.
/// </summary>
[TestClass]
public sealed class QualifiedReDimExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RedimensionsAnArrayInsideAUdtElement()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Type TYPE_SECTION
                Name As String
                Bytes() As Byte
            End Type

            Public Sub Main()
                Dim Section(0 To 2) As TYPE_SECTION

                ReDim Section(0).Bytes(0 To 4)
                Section(0).Bytes(2) = 7
                Debug.Print Section(0).Bytes(2)
                Debug.Print UBound(Section(0).Bytes)

                ReDim Preserve Section(0).Bytes(0 To 6) As Byte
                Debug.Print Section(0).Bytes(2)
                Debug.Print UBound(Section(0).Bytes)
            End Sub
            """,
            "7",
            "4",
            "7",
            "6");
    }

    /// <summary>Each element keeps its own array, which is what makes the member dynamic rather than shared.</summary>
    [TestMethod]
    public void EmitManagedApplication_KeepsMemberArraysSeparatePerElement()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Type TYPE_SECTION
                Bytes() As Byte
            End Type

            Public Sub Main()
                Dim Section(0 To 1) As TYPE_SECTION

                ReDim Section(0).Bytes(0 To 1)
                ReDim Section(1).Bytes(0 To 3)

                Section(0).Bytes(0) = 1
                Section(1).Bytes(0) = 2

                Debug.Print Section(0).Bytes(0)
                Debug.Print Section(1).Bytes(0)
                Debug.Print UBound(Section(1).Bytes)
            End Sub
            """,
            "1",
            "2",
            "3");
    }

    [TestMethod]
    public void EmitManagedApplication_ErasesAnArrayInsideAWithUdtMember()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Type TYPE_SECTION
                Bytes() As Byte
            End Type

            Public Sub Main()
                Dim section As TYPE_SECTION
                ReDim section.Bytes(1 To 2)
                section.Bytes(1) = 7

                With section
                    Erase .Bytes
                End With

                ReDim section.Bytes(3 To 4)
                Debug.Print LBound(section.Bytes)
                Debug.Print UBound(section.Bytes)
            End Sub
            """,
            "3",
            "4");
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
