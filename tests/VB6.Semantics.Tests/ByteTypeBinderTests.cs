using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ByteTypeBinderTests
{
    [TestMethod]
    public void Bind_ConvertsIntegerLiteralToByteOnAssignment()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Byte
                value = 255
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var conversion = (BoundConversionExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Byte, conversion.TargetType);
        Assert.AreEqual(TypeSymbol.Integer, conversion.Expression.Type);
    }

    [TestMethod]
    public void Bind_ByteAndByteArithmeticRemainsByte()
    {
        var model = BindSource("""
            Sub Main()
                Dim left As Byte
                Dim right As Byte
                Dim result As Byte
                result = left + right
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[3];
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Byte, add.Type);
        Assert.AreEqual(TypeSymbol.Byte, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Byte, add.Right.Type);
    }

    [TestMethod]
    public void Bind_UnaryMinusPromotesByteToInteger()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Byte
                Dim result As Integer
                result = -value
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[2];
        var unary = (BoundUnaryExpression)assignment.Expression;
        Assert.AreEqual(SyntaxKind.MinusToken, unary.OperatorKind);
        Assert.AreEqual(TypeSymbol.Integer, unary.Type);
        Assert.AreEqual(TypeSymbol.Integer, unary.Operand.Type);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
