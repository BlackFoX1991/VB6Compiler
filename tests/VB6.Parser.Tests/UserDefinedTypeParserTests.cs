using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class UserDefinedTypeParserTests
{
    [TestMethod]
    public void Parse_RecognizesTypeDeclarationFieldsAndFixedString()
    {
        var root = Parse("""
            Private Type Point
                X As Long
                Name As String * 16
                Values(1 To 2) As Integer
            End Type
            """);

        var declaration = (TypeDeclarationSyntax)root.Members.Single();
        Assert.AreEqual("Private", declaration.VisibilityKeyword!.Text);
        Assert.AreEqual("Point", declaration.Identifier.Text);
        Assert.AreEqual(3, declaration.Fields.Length);
        Assert.AreEqual("X", declaration.Fields[0].Identifier.Text);
        Assert.AreEqual("Name", declaration.Fields[1].Identifier.Text);
        Assert.IsNotNull(declaration.Fields[1].FixedStringStarToken);
        Assert.AreEqual("16", ((LiteralExpressionSyntax)declaration.Fields[1].FixedStringLength!).LiteralToken.Text);
        Assert.IsTrue(declaration.Fields[2].IsArray);
    }

    [TestMethod]
    public void Parse_RecognizesMemberAccessAssignmentAndExpression()
    {
        var root = Parse("""
            Sub Main()
                point.X = 10
                point.Values(1) = 20
                Debug.Print point.X
                Debug.Print point.Values(1)
            End Sub
            """);

        var procedure = (SubDeclarationSyntax)root.Members.Single();
        var assignment = (AssignmentStatementSyntax)procedure.Statements[0];
        Assert.IsTrue(assignment.IsMember);
        Assert.AreEqual("point", assignment.Identifier.Text);
        Assert.AreEqual("X", assignment.MemberIdentifier!.Text);

        var arrayAssignment = (AssignmentStatementSyntax)procedure.Statements[1];
        Assert.IsTrue(arrayAssignment.IsMember);
        Assert.IsTrue(arrayAssignment.IsIndexed);
        Assert.AreEqual("Values", arrayAssignment.MemberIdentifier!.Text);

        var print = (DebugPrintStatementSyntax)procedure.Statements[2];
        var access = (MemberAccessExpressionSyntax)print.Expression;
        Assert.AreEqual("X", access.Identifier.Text);

        var arrayPrint = (DebugPrintStatementSyntax)procedure.Statements[3];
        var arrayAccess = (MemberAccessExpressionSyntax)arrayPrint.Expression;
        Assert.IsTrue(arrayAccess.IsIndexed);
        Assert.AreEqual("Values", arrayAccess.Identifier.Text);
    }

    [TestMethod]
    public void Parse_RecognizesWithBlockImplicitMemberAccess()
    {
        var root = Parse("""
            Sub Main()
                With point
                    .X = 10
                    .Values(1) = 20
                    Debug.Print .X
                    Debug.Print .Values(1)
                End With
            End Sub
            """);

        var procedure = (SubDeclarationSyntax)root.Members.Single();
        var withStatement = (WithStatementSyntax)procedure.Statements.Single();
        Assert.AreEqual("point", ((NameExpressionSyntax)withStatement.Target).IdentifierToken.Text);
        Assert.AreEqual(4, withStatement.Statements.Length);

        var assignment = (ImplicitMemberAssignmentStatementSyntax)withStatement.Statements[0];
        Assert.AreEqual("X", assignment.MemberIdentifier.Text);

        var arrayAssignment = (ImplicitMemberAssignmentStatementSyntax)withStatement.Statements[1];
        Assert.IsTrue(arrayAssignment.IsIndexed);
        Assert.AreEqual("Values", arrayAssignment.MemberIdentifier.Text);

        var print = (DebugPrintStatementSyntax)withStatement.Statements[2];
        var access = (ImplicitMemberAccessExpressionSyntax)print.Expression;
        Assert.AreEqual("X", access.Identifier.Text);

        var arrayPrint = (DebugPrintStatementSyntax)withStatement.Statements[3];
        var arrayAccess = (ImplicitMemberAccessExpressionSyntax)arrayPrint.Expression;
        Assert.IsTrue(arrayAccess.IsIndexed);
        Assert.AreEqual("Values", arrayAccess.Identifier.Text);
    }

    [TestMethod]
    public void Parse_RecognizesNestedMemberAssignmentTarget()
    {
        var root = Parse("""
            Sub Main()
                outer.Inner.Value = 10
            End Sub
            """);

        var procedure = (SubDeclarationSyntax)root.Members.Single();
        var assignment = (AssignmentStatementSyntax)procedure.Statements.Single();
        var valueAccess = (MemberAccessExpressionSyntax)assignment.Target!;
        Assert.AreEqual("Value", valueAccess.Identifier.Text);
        var innerAccess = (MemberAccessExpressionSyntax)valueAccess.Target;
        Assert.AreEqual("Inner", innerAccess.Identifier.Text);
    }

    private static CompilationUnitSyntax Parse(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        return result.Root;
    }
}
