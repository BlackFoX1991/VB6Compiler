using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class UserDefinedTypeMemberArrayAssignmentTests
{
    [TestMethod]
    public void Bind_ResolvesUdtArrayMemberWrite()
    {
        var model = Bind("""
            Type Record
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim record As Record
                record.Values(2) = 9
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var assignment = model.Procedures.Single().Body.Statements
            .OfType<BoundMemberAssignmentStatement>()
            .Single();
        var target = assignment.Target as BoundElementAccessExpression;
        Assert.IsNotNull(target);
        Assert.AreSame(TypeSymbol.Long, target.Type);
        Assert.AreSame(TypeSymbol.Long, assignment.Expression.Type);

        var member = target.Receiver as BoundMemberAccessExpression;
        Assert.IsNotNull(member);
        Assert.AreEqual("Values", member.Member.Name);
    }

    [TestMethod]
    public void Bind_ResolvesWithUdtArrayMemberWrite()
    {
        var model = Bind("""
            Type Record
                Values(1 To 3) As Long
            End Type

            Sub Main()
                Dim record As Record
                With record
                    .Values(1) = 5
                End With
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var withStatement = model.Procedures.Single().Body.Statements
            .OfType<BoundWithStatement>()
            .Single();
        var assignment = withStatement.Body.Statements
            .OfType<BoundMemberAssignmentStatement>()
            .Single();
        var target = assignment.Target as BoundElementAccessExpression;
        Assert.IsNotNull(target);

        var member = target.Receiver as BoundMemberAccessExpression;
        Assert.IsNotNull(member);
        Assert.IsInstanceOfType<BoundWithReceiverExpression>(member.Receiver);
    }

    [TestMethod]
    public void Bind_ResolvesMemberWriteAfterIndexedUdtMember()
    {
        var model = Bind("""
            Type Child
                Value As Long
            End Type

            Type Parent
                Children(1 To 2) As Child
            End Type

            Sub Main()
                Dim record As Parent
                record.Children(1).Value = 3
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var assignment = model.Procedures.Single().Body.Statements
            .OfType<BoundMemberAssignmentStatement>()
            .Single();
        var target = assignment.Target as BoundMemberAccessExpression;
        Assert.IsNotNull(target);
        Assert.AreEqual("Value", target.Member.Name);
        Assert.IsInstanceOfType<BoundElementAccessExpression>(target.Receiver);
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
