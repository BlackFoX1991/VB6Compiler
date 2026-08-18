using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ParserTests
{
    [TestMethod]
    public void Parse_FirstAcceptanceProgram()
    {
        const string source = """
            Option Explicit

            Sub Main()
                Dim x As Integer
                x = 10

                If x > 5 Then
                    Debug.Print x
                End If
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.AreEqual(2, result.Root.Members.Length);
        Assert.IsInstanceOfType<OptionExplicitSyntax>(result.Root.Members[0]);

        var sub = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual("Main", sub.Identifier.Text);
        Assert.AreEqual(0, sub.Parameters.Length);
        Assert.AreEqual(3, sub.Statements.Length);
        Assert.IsInstanceOfType<DimStatementSyntax>(sub.Statements[0]);
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(sub.Statements[1]);
        Assert.IsInstanceOfType<IfStatementSyntax>(sub.Statements[2]);

        var ifStatement = (IfStatementSyntax)sub.Statements[2];
        Assert.AreEqual(1, ifStatement.Statements.Length);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(ifStatement.Statements[0]);
    }

    [TestMethod]
    public void Parse_UsesOperatorPrecedence()
    {
        const string source = """
            Sub Main()
                x = 1 + 2 * 3
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var assignment = (AssignmentStatementSyntax)sub.Statements.Single();
        var rootBinary = (BinaryExpressionSyntax)assignment.Expression;

        Assert.AreEqual(SyntaxKind.PlusToken, rootBinary.OperatorToken.Kind);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(rootBinary.Right);
        Assert.AreEqual(SyntaxKind.StarToken, ((BinaryExpressionSyntax)rootBinary.Right).OperatorToken.Kind);
    }

    [TestMethod]
    public void Parse_RecognizesBareProcedureCall()
    {
        const string source = """
            Sub Main()
                Helper
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var invocation = (InvocationStatementSyntax)sub.Statements.Single();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.IsNull(invocation.CallKeyword);
        Assert.AreEqual("Helper", invocation.Identifier.Text);
        Assert.IsNull(invocation.OpenParenthesisToken);
        Assert.AreEqual(0, invocation.Arguments.Length);
    }

    [TestMethod]
    public void Parse_RecognizesCallKeywordWithParentheses()
    {
        const string source = """
            Sub Main()
                Call Helper()
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var invocation = (InvocationStatementSyntax)sub.Statements.Single();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.IsNotNull(invocation.CallKeyword);
        Assert.AreEqual(SyntaxKind.CallKeyword, invocation.CallKeyword!.Kind);
        Assert.AreEqual("Helper", invocation.Identifier.Text);
        Assert.IsNotNull(invocation.OpenParenthesisToken);
        Assert.AreEqual(0, invocation.Arguments.Length);
        Assert.IsNotNull(invocation.CloseParenthesisToken);
    }

    [TestMethod]
    public void Parse_RecognizesParametersAndArguments()
    {
        const string source = """
            Sub Update(ByRef value As Integer, ByVal copy As Integer, implicitRef As Integer)
            End Sub

            Sub Main()
                Call Update(x, 2, x)
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length);

        var update = (SubDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual(3, update.Parameters.Length);
        Assert.AreEqual(SyntaxKind.ByRefKeyword, update.Parameters[0].PassingModeKeyword!.Kind);
        Assert.AreEqual(SyntaxKind.ByValKeyword, update.Parameters[1].PassingModeKeyword!.Kind);
        Assert.IsNull(update.Parameters[2].PassingModeKeyword);

        var main = (SubDeclarationSyntax)result.Root.Members[1];
        var invocation = (InvocationStatementSyntax)main.Statements.Single();
        Assert.AreEqual(3, invocation.Arguments.Length);
        Assert.IsInstanceOfType<NameExpressionSyntax>(invocation.Arguments[0]);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(invocation.Arguments[1]);
    }

    [TestMethod]
    public void Parse_RecognizesBareCallArguments()
    {
        const string source = """
            Sub Main()
                Helper x, 10
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var invocation = (InvocationStatementSyntax)sub.Statements.Single();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.AreEqual(2, invocation.Arguments.Length);
    }

    [TestMethod]
    public void Parse_RecognizesFunctionDeclarationAndInvocationExpression()
    {
        const string source = """
            Function Add(ByVal left As Integer, ByVal right As Integer) As Integer
                Add = left + right
            End Function

            Sub Main()
                Dim result As Integer
                result = Add(5, 7)
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length);

        var function = (FunctionDeclarationSyntax)result.Root.Members[0];
        Assert.AreEqual("Add", function.Identifier.Text);
        Assert.AreEqual(2, function.Parameters.Length);
        Assert.AreEqual("Integer", function.ReturnTypeToken.Text);
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(function.Statements.Single());

        var main = (SubDeclarationSyntax)result.Root.Members[1];
        var assignment = (AssignmentStatementSyntax)main.Statements[1];
        var invocation = (InvocationExpressionSyntax)assignment.Expression;
        Assert.AreEqual("Add", invocation.Identifier.Text);
        Assert.AreEqual(2, invocation.Arguments.Length);
    }

    [TestMethod]
    public void Parse_ReportsMissingEndSub()
    {
        const string source = """
            Sub Main()
                Dim x As Integer
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6P0001"));
    }
}
