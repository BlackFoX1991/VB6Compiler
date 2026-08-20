using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class LongTypeParserTests
{
    [TestMethod]
    public void Parse_RecognizesLongInLocalsParametersAndReturnTypes()
    {
        const string source = """
            Function AddLong(ByVal left As Long, ByVal right As Long) As Long
                Dim value As Long
                value = left + right
                AddLong = value
            End Function
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(SyntaxKind.LongKeyword, function.Parameters[0].TypeToken.Kind);
        Assert.AreEqual(SyntaxKind.LongKeyword, function.Parameters[1].TypeToken.Kind);
        Assert.AreEqual(SyntaxKind.LongKeyword, function.ReturnTypeToken!.Kind);
        Assert.AreEqual(SyntaxKind.LongKeyword, ((DimStatementSyntax)function.Statements[0]).TypeToken.Kind);
    }
}
