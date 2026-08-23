using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class AddressOfParserTests
{
    [TestMethod]
    public void Parse_RecognizesAddressOfCallbackArgument()
    {
        const string source = """
            Sub Main()
                SetWindowLong hwnd, 0, AddressOf WindowProc
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var invocation = (InvocationStatementSyntax)procedure.Statements.Single();
        var addressOf = (AddressOfExpressionSyntax)invocation.Arguments[2];

        Assert.AreEqual("AddressOf", addressOf.AddressOfKeyword.Text);
        Assert.AreEqual("WindowProc", addressOf.TargetToken.Text);
    }
}
