using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class IfBranchBinderTests
{
    [TestMethod]
    public void Bind_BindsElseIfAndElseBranches()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                If x = 1 Then
                    x = 10
                ElseIf x = 2 Then
                    x = 20
                Else
                    x = 30
                End If
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var statement = (BoundIfStatement)model.Procedures.Single().Body.Statements[1];

        Assert.AreEqual(TypeSymbol.Boolean, statement.Condition.Type);
        Assert.AreEqual(1, statement.ElseIfClauses.Length);
        Assert.AreEqual(TypeSymbol.Boolean, statement.ElseIfClauses[0].Condition.Type);
        Assert.IsNotNull(statement.ElseBody);
        Assert.AreEqual(1, statement.ElseBody!.Statements.Length);
    }

    [TestMethod]
    public void Bind_SingleLineIfUsesSameBoundShape()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                If x = 1 Then x = 2 Else x = 3
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var statement = (BoundIfStatement)model.Procedures.Single().Body.Statements[1];

        Assert.AreEqual(1, statement.Body.Statements.Length);
        Assert.AreEqual(0, statement.ElseIfClauses.Length);
        Assert.IsNotNull(statement.ElseBody);
        Assert.AreEqual(1, statement.ElseBody!.Statements.Length);
    }

    [TestMethod]
    public void Bind_PredeclaresLocalsFromAllIfBranches()
    {
        var model = BindSource("""
            Sub Main()
                If 1 = 1 Then
                    Dim fromThen As Integer
                ElseIf 1 = 2 Then
                    Dim fromElseIf As Integer
                Else
                    Dim fromElse As Integer
                End If
                fromThen = 1
                fromElseIf = 2
                fromElse = 3
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var locals = model.Procedures.Single().Locals.Select(local => local.Name).ToArray();
        CollectionAssert.Contains(locals, "fromThen");
        CollectionAssert.Contains(locals, "fromElseIf");
        CollectionAssert.Contains(locals, "fromElse");
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
