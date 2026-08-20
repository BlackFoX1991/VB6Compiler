using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class FileIoParserTests
{
    [TestMethod]
    public void Parse_PreservesClassicFileIoStatementsWithoutParserErrors()
    {
        const string source = """
            Sub Main()
                Open "data.bin" For Binary As #1
                Put #1, 1, value
                Get #1, 1, value
                Seek #1, 4
                Close #1
                Kill "data.bin"
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(6, procedure.Statements.Length);
        Assert.IsTrue(procedure.Statements.All(statement => statement is FileIoStatementSyntax));
        CollectionAssert.AreEqual(
            new[] { "Open", "Put", "Get", "Seek", "Close", "Kill" },
            procedure.Statements
                .Cast<FileIoStatementSyntax>()
                .Select(statement => statement.KeywordToken.Text)
                .ToArray());
    }

    [TestMethod]
    public void Parse_PreservesPrintHashStatement()
    {
        const string source = """
            Sub Main()
                Print #1, "hello"
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.IsInstanceOfType<FileIoStatementSyntax>(procedure.Statements.Single());
    }

    [TestMethod]
    public void Parse_DoesNotStealUserProcedureNamedGet()
    {
        const string source = """
            Sub Get(value As Long)
            End Sub

            Sub Main()
                Get 42
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var main = (SubDeclarationSyntax)result.Root.Members.Last();
        Assert.IsInstanceOfType<InvocationStatementSyntax>(main.Statements.Single());
    }
}
