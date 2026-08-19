using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class SelectCaseBinderTests
{
    [TestMethod]
    public void Bind_BindsSelectCaseClausesToSelectorType()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                Select Case x
                    Case "1"
                        Debug.Print 1
                    Case 2 To 4
                        Debug.Print 2
                    Case Is >= 5
                        Debug.Print 3
                    Case Else
                        Debug.Print 4
                End Select
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var select = (BoundSelectCaseStatement)model.Procedures.Single().Body.Statements[1];
        Assert.AreEqual(TypeSymbol.Integer, select.Expression.Type);
        Assert.AreEqual(4, select.Cases.Length);

        var valueClause = (BoundCaseValueClause)select.Cases[0].Clauses.Single();
        Assert.AreEqual(TypeSymbol.Integer, valueClause.Value.Type);
        Assert.IsInstanceOfType<BoundConversionExpression>(valueClause.Value);

        var rangeClause = (BoundCaseRangeClause)select.Cases[1].Clauses.Single();
        Assert.AreEqual(TypeSymbol.Integer, rangeClause.LowerBound.Type);
        Assert.AreEqual(TypeSymbol.Integer, rangeClause.UpperBound.Type);

        var relationalClause = (BoundCaseRelationalClause)select.Cases[2].Clauses.Single();
        Assert.AreEqual(TypeSymbol.Integer, relationalClause.Value.Type);
        Assert.IsInstanceOfType<BoundCaseElseClause>(select.Cases[3].Clauses.Single());
    }

    [TestMethod]
    public void Bind_ReportsCaseElseThatIsNotLast()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                Select Case x
                    Case Else
                        Debug.Print 1
                    Case 2
                        Debug.Print 2
                End Select
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0016"));
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
