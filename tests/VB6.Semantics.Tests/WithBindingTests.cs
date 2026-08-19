using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class WithBindingTests
{
    [TestMethod]
    public void Bind_ResolvesImplicitMembersAgainstWithReceiver()
    {
        var model = Bind("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim point As Point
                Dim result As Long
                With point
                    .X = 41
                    result = .X
                End With
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var withStatement = model.Procedures.Single().Body.Statements.OfType<BoundWithStatement>().Single();
        Assert.AreEqual(0, withStatement.WithId);
        Assert.IsInstanceOfType<BoundVariableExpression>(withStatement.Target);

        var write = withStatement.Body.Statements.OfType<BoundMemberAssignmentStatement>().Single();
        var writeMember = write.Target as BoundMemberAccessExpression;
        Assert.IsNotNull(writeMember);
        var writeReceiver = writeMember.Receiver as BoundWithReceiverExpression;
        Assert.IsNotNull(writeReceiver);
        Assert.AreEqual(withStatement.WithId, writeReceiver.WithId);

        var assignment = withStatement.Body.Statements.OfType<BoundAssignmentStatement>().Single();
        var readMember = assignment.Expression as BoundMemberAccessExpression;
        Assert.IsNotNull(readMember);
        Assert.AreSame(writeMember.Member, readMember.Member);
    }

    [TestMethod]
    public void Bind_NestedWithTargetsOuterImplicitMember()
    {
        var model = Bind("""
            Type Inner
                Value As Long
            End Type

            Type Outer
                Child As Inner
            End Type

            Sub Main()
                Dim outer As Outer
                With outer
                    With .Child
                        .Value = 7
                    End With
                End With
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var outerWith = model.Procedures.Single().Body.Statements.OfType<BoundWithStatement>().Single();
        var innerWith = outerWith.Body.Statements.OfType<BoundWithStatement>().Single();
        Assert.AreEqual(0, outerWith.WithId);
        Assert.AreEqual(1, innerWith.WithId);

        var innerTarget = innerWith.Target as BoundMemberAccessExpression;
        Assert.IsNotNull(innerTarget);
        var outerReceiver = innerTarget.Receiver as BoundWithReceiverExpression;
        Assert.IsNotNull(outerReceiver);
        Assert.AreEqual(outerWith.WithId, outerReceiver.WithId);

        var write = innerWith.Body.Statements.OfType<BoundMemberAssignmentStatement>().Single();
        var valueMember = write.Target as BoundMemberAccessExpression;
        Assert.IsNotNull(valueMember);
        var innerReceiver = valueMember.Receiver as BoundWithReceiverExpression;
        Assert.IsNotNull(innerReceiver);
        Assert.AreEqual(innerWith.WithId, innerReceiver.WithId);
    }

    [TestMethod]
    public void Bind_PredeclaresLocalsInsideWith()
    {
        var model = Bind("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim point As Point
                With point
                    value = .X
                    Dim value As Long
                End With
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var main = model.Procedures.Single();
        Assert.IsTrue(main.Locals.Any(local => local.Name == "value"));
    }

    [TestMethod]
    public void Bind_ReportsImplicitMemberOutsideWith()
    {
        var analysis = Analyze("""
            Sub Main()
                .X = 1
            End Sub
            """);

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0049"));
    }

    [TestMethod]
    public void Bind_ReportsNonUdtWithTarget()
    {
        var analysis = Analyze("""
            Sub Main()
                Dim value As Long
                With value
                    .X = 1
                End With
            End Sub
            """);

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0050"));
    }

    [TestMethod]
    public void Bind_ReportsNonAddressableUdtWithTarget()
    {
        var analysis = Analyze("""
            Type Point
                X As Long
            End Type

            Function MakePoint() As Point
                Dim value As Point
                MakePoint = value
            End Function

            Sub Main()
                With MakePoint()
                    .X = 1
                End With
            End Sub
            """);

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0051"));
    }

    private static SemanticModel Bind(string source)
    {
        var analysis = Analyze(source);
        Assert.IsNotNull(analysis.SemanticModel, FormatDiagnostics(analysis));
        return analysis.SemanticModel;
    }

    private static TestAnalysis Analyze(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parse = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parse.Diagnostics.Length, string.Join(Environment.NewLine, parse.Diagnostics));
        var types = new UserDefinedTypeDeclarationBinder(text).Bind(parse.Root);
        using (UserDefinedTypeLookupScope.Push(types.Types))
        {
            var model = new Binder(text).BindCompilationUnit(parse.Root);
            return new TestAnalysis(model, types.Diagnostics.AddRange(model.Diagnostics));
        }
    }

    private static string FormatDiagnostics(SemanticModel model) =>
        string.Join(Environment.NewLine, model.Diagnostics.Select(diagnostic => diagnostic.ToString()));

    private static string FormatDiagnostics(TestAnalysis analysis) =>
        string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()));

    private sealed record TestAnalysis(
        SemanticModel? SemanticModel,
        ImmutableArray<Diagnostic> Diagnostics);
}