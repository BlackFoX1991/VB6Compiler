using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class FloatingTypeBinderTests
{
    [TestMethod]
    public void Bind_ConvertsUnsuffixedFloatingLiteralToSingleOnAssignment()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Single
                value = 1.5
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var conversion = (BoundConversionExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Single, conversion.TargetType);
        Assert.AreEqual(TypeSymbol.Double, conversion.Expression.Type);
    }

    [TestMethod]
    public void Bind_PromotesSingleAndIntegerToSingle()
    {
        var model = BindSource("""
            Sub Main()
                Dim left As Single
                Dim result As Single
                left = 1.5
                result = left + 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[3];
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Single, add.Type);
        Assert.AreEqual(TypeSymbol.Single, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Single, add.Right.Type);
    }

    [TestMethod]
    public void Bind_PromotesSingleAndLongToDouble()
    {
        var model = BindSource("""
            Sub Main()
                Dim left As Single
                Dim right As Long
                Dim result As Double
                result = left + right
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[3];
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Double, add.Type);
        Assert.AreEqual(TypeSymbol.Double, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Double, add.Right.Type);
    }

    /// <summary>
    /// <c>/</c> computes in Double, and the assignment target does not change that -- the operand
    /// types decide alone, which is the same rule that makes <c>value = 2000 * 365</c> overflow.
    ///
    /// This case asserted <c>Single</c> until 2026-09-10, when VB6 SP6 said otherwise. It was a
    /// regression proof for an expectation nobody had measured.
    /// </summary>
    [TestMethod]
    public void Bind_IntegerFloatingDivisionProducesDouble()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Single
                value = 1 / 2
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];

        // Die Zuweisung an ein Single schiebt jetzt eine Konvertierung dazwischen -- genau das ist
        // der Befund: Der Ausdruck rechnet in Double und wird erst danach schmaler.
        var divide = (BoundBinaryExpression)Unwrap(assignment.Expression);
        Assert.AreEqual(TypeSymbol.Double, divide.Type);
        Assert.AreEqual(TypeSymbol.Double, divide.Left.Type);
        Assert.AreEqual(TypeSymbol.Double, divide.Right.Type);
    }

    /// <summary>
    /// The exception, and its edge. A Single on one side makes the result Single only while the
    /// other side is no wider -- measured across all 121 operand pairs against the original, where
    /// <c>Single / Long</c>, <c>Single / Double</c>, <c>Single / Currency</c> and
    /// <c>Single / Date</c> all came back Double.
    /// </summary>
    [TestMethod]
    public void Bind_DivisionKeepsSingleOnlyBesideANarrowerOperand()
    {
        var model = BindSource("""
            Sub Main()
                Dim s As Single
                Dim i As Integer
                Dim l As Long
                Dim b As Boolean
                Dim r As Double
                r = s / i
                r = i / s
                r = s / b
                r = s / l
                r = i / i
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var statements = model.Procedures.Single().Body.Statements;
        var types = Enumerable.Range(5, 5)
            .Select(index => ((BoundBinaryExpression)
                Unwrap(((BoundAssignmentStatement)statements[index]).Expression)).Type)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                TypeSymbol.Single,
                TypeSymbol.Single,
                TypeSymbol.Single,
                TypeSymbol.Double,
                TypeSymbol.Double
            },
            types);
    }

    /// <summary>Strips the conversion an assignment adds, so the operator's own type is visible.</summary>
    private static BoundExpression Unwrap(BoundExpression expression) =>
        expression is BoundConversionExpression conversion ? conversion.Expression : expression;

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
