using VB6.Syntax.Nodes;
using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// File statements are recognized at statement position only - reserving Open, Close, Get, Put,
/// Print and Seek globally would repeat what Option Base already taught about VB6 context words.
/// </summary>
[TestClass]
public sealed class FileStatementParserTests
{
    [TestMethod]
    public void Parse_OpenForBinaryWithFileNumber()
    {
        var statement = ParseSingleStatement("""
            Open sFile For Binary As #FileNum
            """);

        var open = (OpenStatementSyntax)statement;
        Assert.AreEqual("Binary", open.ModeToken!.Text);
        Assert.IsNotNull(open.FileNumber.HashToken);
        Assert.IsInstanceOfType<NameExpressionSyntax>(open.FileNumber.Expression);
        Assert.IsNull(open.RecordLength);
    }

    [TestMethod]
    public void Parse_OpenWithRecordLength()
    {
        var open = (OpenStatementSyntax)ParseSingleStatement("""
            Open sFile For Random As #1 Len = 128
            """);

        Assert.IsNotNull(open.LenKeyword);
        Assert.IsNotNull(open.RecordLength);
    }

    [TestMethod]
    public void Parse_OpenPreservesAccessAndSharingClauses()
    {
        var open = (OpenStatementSyntax)ParseSingleStatement("""
            Open sFile For Binary Access Read Write Lock Read As #1
            """);

        Assert.AreEqual("Read Write", string.Join(" ", open.AccessTokens.Select(token => token.Text)));
        Assert.AreEqual("Lock Read", string.Join(" ", open.SharingTokens.Select(token => token.Text)));
    }

    [TestMethod]
    public void Parse_OpenAllowsOmittedMode()
    {
        var open = (OpenStatementSyntax)ParseSingleStatement("Open sFile As #1");

        Assert.IsNull(open.ForKeyword);
        Assert.IsNull(open.ModeToken);
    }

    [TestMethod]
    public void Parse_TextOpenModesAndFilePrint()
    {
        var open = (OpenStatementSyntax)ParseSingleStatement("Open sFile For Append As #1");
        Assert.AreEqual("Append", open.ModeToken!.Text);

        var print = (FilePrintStatementSyntax)ParseSingleStatement("Print #1, value");
        Assert.IsNotNull(print.FileNumber.HashToken);
        Assert.IsInstanceOfType<NameExpressionSyntax>(print.Expression);
    }

    [TestMethod]
    public void Parse_FilePrintAllowsAnEmptyOutputList()
    {
        var print = (FilePrintStatementSyntax)ParseSingleStatement("Print #1,");

        Assert.IsNull(print.Expression);
    }

    [TestMethod]
    public void Parse_FilePrintPreservesOutputListSeparators()
    {
        var print = (FilePrintStatementSyntax)ParseSingleStatement("Print #1, first; second, third;");

        Assert.AreEqual(3, print.Expressions.Length);
        Assert.AreEqual(SyntaxKind.SemicolonToken, print.Separators[0].Kind);
        Assert.AreEqual(SyntaxKind.CommaToken, print.Separators[1].Kind);
        Assert.AreEqual(SyntaxKind.SemicolonToken, print.Separators[2].Kind);
    }

    [TestMethod]
    public void Parse_WidthStatementPreservesFileNumberAndExpression()
    {
        var width = (WidthStatementSyntax)ParseSingleStatement("Width #1, 80");

        Assert.IsNotNull(width.FileNumber.HashToken);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(width.Width);
    }

    [TestMethod]
    public void Parse_QualifiedWidthStatementPreservesWidthKeyword()
    {
        var width = (WidthStatementSyntax)ParseSingleStatement("VBA.Width 1, 80");

        Assert.AreEqual("Width", width.WidthKeyword.Text);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(width.Width);
    }

    [TestMethod]
    public void Parse_LineInputWithFileNumberAndTarget()
    {
        var lineInput = (LineInputStatementSyntax)ParseSingleStatement("Line Input #1, text");

        Assert.IsNotNull(lineInput.FileNumber.HashToken);
        Assert.IsInstanceOfType<NameExpressionSyntax>(lineInput.Target);
    }

    [TestMethod]
    public void Parse_InputWithMultipleStringTargets()
    {
        var input = (FileInputStatementSyntax)ParseSingleStatement("Input #1, first, second");

        Assert.AreEqual(2, input.Targets.Length);
        Assert.IsInstanceOfType<NameExpressionSyntax>(input.Targets[1]);
    }

    [TestMethod]
    public void Parse_CloseAcceptsSeveralFileNumbers()
    {
        var close = (CloseStatementSyntax)ParseSingleStatement("Close #1, #2");

        Assert.AreEqual(2, close.FileNumbers.Length);
    }

    [TestMethod]
    public void Parse_CloseWithoutFileNumberClosesEverything()
    {
        var close = (CloseStatementSyntax)ParseSingleStatement("Close");

        Assert.AreEqual(0, close.FileNumbers.Length);
    }

    [TestMethod]
    public void Parse_GetAndPutCarryPositionAndTarget()
    {
        var get = (GetStatementSyntax)ParseSingleStatement("Get #1, 5, Buffer");
        Assert.IsNotNull(get.RecordPosition);
        Assert.IsInstanceOfType<NameExpressionSyntax>(get.Target);

        var put = (PutStatementSyntax)ParseSingleStatement("Put #1, 5, Buffer");
        Assert.IsNotNull(put.RecordPosition);
    }

    /// <summary>VB6 allows the record position to be omitted, continuing at the current position.</summary>
    [TestMethod]
    public void Parse_GetWithoutRecordPosition()
    {
        var get = (GetStatementSyntax)ParseSingleStatement("Get #1, , Buffer");

        Assert.IsNull(get.RecordPosition);
        Assert.IsInstanceOfType<NameExpressionSyntax>(get.Target);
    }

    [TestMethod]
    public void Parse_Seek()
    {
        var seek = (SeekStatementSyntax)ParseSingleStatement("Seek #1, 100");

        Assert.IsNotNull(seek.FileNumber.HashToken);
    }

    /// <summary>
    /// Outside statement position these stay ordinary identifiers, which is why an assignment to a
    /// variable called Get must not be read as a file statement.
    /// </summary>
    [TestMethod]
    public void Parse_TreatsTheWordsAsIdentifiersWhenAssignedTo()
    {
        var statement = ParseSingleStatement("Get = 3");

        Assert.IsInstanceOfType<AssignmentStatementSyntax>(statement);
    }

    private static StatementSyntax ParseSingleStatement(string statement)
    {
        var source = $"""
            Sub Main()
                {statement}
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(", ", result.Diagnostics.Select(d => d.Message)));

        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return procedure.Statements.Single();
    }
}
