namespace VB6.Compiler.Tests;

/// <summary>
/// The two legacy statement forms the grammar inventory found missing.
///
/// <c>Rem</c> is the older of BASIC's two comment introducers and is common in code that predates
/// the apostrophe; <c>Let</c> is the explicit form of an ordinary assignment, the counterpart of
/// <c>Set</c>. Neither was accepted: <c>Rem</c> reached the parser as an identifier and produced
/// <c>VB6P0001</c>, and <c>Let</c> was bound as a call to an undeclared procedure, <c>VB6S0005</c>.
///
/// <c>Rem</c> is recognised in the lexer rather than the parser, and the awkward-text case below is
/// why: by the time a statement reaches the parser, an apostrophe or an unpaired quote inside the
/// comment has already been lexed, and an unpaired quote is not something the parser can take back.
/// </summary>
[TestClass]
public sealed class RemAndLetStatementExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_TreatsRemAsAComment()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Dim a As Long

                Rem a comment on its own line
                a = 1
                a = a + 1: Rem after a colon
                Debug.Print "value|" & a

                GoTo 20
                a = 999
            20 Rem after a line number
                Debug.Print "jumped|" & a
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "value|2", "jumped|2" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LetsRemCommentsHoldQuotesAndApostrophes()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Rem it's got an apostrophe and an unpaired " quote
                Debug.Print "survived"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "survived" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsRemAnIdentifierWhereItIsNotAStatement()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Dim Remainder As Long
                Dim s As String

                Remainder = 5
                s = "Rem is fine inside a string"

                Debug.Print "identifier|" & Remainder
                Debug.Print "string|" & s
            End Sub
            """);

        // A word that merely begins with Rem is an ordinary identifier, and a statement start is
        // the only place the comment form applies.
        CollectionAssert.AreEqual(
            new[] { "identifier|5", "string|Rem is fine inside a string" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_AcceptsLetOnEveryAssignableForm()
    {
        var output = VB6TestProgram.RunLines("""
            Private Type Slot
                Value As Long
            End Type

            Private Slots(0 To 2) As Slot
            Private Numbers(0 To 2) As Long

            Public Sub Main()
                Dim a As Long

                Let a = 1
                Let Numbers(1) = 2
                Let Slots(1).Value = 3
                Let a = a + 10

                Debug.Print "let|" & a & "," & Numbers(1) & "," & Slots(1).Value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "let|11,2,3" }, output);
    }
}
