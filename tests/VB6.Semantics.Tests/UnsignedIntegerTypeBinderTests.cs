using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class UnsignedIntegerTypeBinderTests
{
    [TestMethod]
    public void Bind_RecognizesUIntegerAndUInt32Aliases()
    {
        var model = BindSource("""
            Function AddOne(ByVal value As UInt32) As UInteger
                AddOne = value + 1
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.UInteger, procedure.Symbol.ReturnType);
        Assert.AreEqual(TypeSymbol.UInteger, procedure.Symbol.Parameters.Single().Type);

        var assignment = (BoundAssignmentStatement)procedure.Body.Statements.Single();
        Assert.AreEqual(TypeSymbol.UInteger, assignment.Expression.Type);
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.UInteger, add.Left.Type);
        Assert.AreEqual(TypeSymbol.UInteger, add.Right.Type);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
