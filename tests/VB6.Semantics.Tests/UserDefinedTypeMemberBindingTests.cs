using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class UserDefinedTypeMemberBindingTests
{
    [TestMethod]
    public void Bind_ResolvesMemberReadsAndAssignmentsCaseInsensitively()
    {
        var model = Bind("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim point As Point
                Dim value As Long
                point.x = 41
                value = point.X
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var body = model.Procedures.Single().Body.Statements;
        var write = body.OfType<BoundMemberAssignmentStatement>().Single();
        var writeTarget = write.Target as BoundMemberAccessExpression;
        Assert.IsNotNull(writeTarget);
        Assert.AreEqual("X", writeTarget.Member.Name);
        Assert.AreSame(TypeSymbol.Long, writeTarget.Type);

        var scalarAssignment = body.OfType<BoundAssignmentStatement>().Single();
        var read = scalarAssignment.Expression as BoundMemberAccessExpression;
        Assert.IsNotNull(read);
        Assert.AreEqual("X", read.Member.Name);
        Assert.AreSame(writeTarget.Member, read.Member);
    }

    [TestMethod]
    public void Bind_ResolvesNestedUdtMemberChain()
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
                outer.Child.Value = 7
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var write = model.Procedures.Single().Body.Statements
            .OfType<BoundMemberAssignmentStatement>()
            .Single();
        var value = write.Target as BoundMemberAccessExpression;
        Assert.IsNotNull(value);
        Assert.AreEqual("Value", value.Member.Name);
        var child = value.Receiver as BoundMemberAccessExpression;
        Assert.IsNotNull(child);
        Assert.AreEqual("Child", child.Member.Name);
    }

    [TestMethod]
    public void Bind_ReportsUnknownUdtMember()
    {
        var analysis = Analyze("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim point As Point
                point.Missing = 1
            End Sub
            """);

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0048"));
    }

    [TestMethod]
    public void Bind_ReportsMemberAccessOnNonUdt()
    {
        var analysis = Analyze("""
            Sub Main()
                Dim value As Long
                value.X = 1
            End Sub
            """);

        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0047"));
    }

    [TestMethod]
    public void Bind_AllowsUdtMemberAsByRefArgument()
    {
        var model = Bind("""
            Type Point
                X As Long
            End Type

            Sub SetValue(ByRef value As Long)
                value = 10
            End Sub

            Sub Main()
                Dim point As Point
                SetValue point.X
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = main.Body.Statements.OfType<BoundInvocationStatement>().Single();
        Assert.IsInstanceOfType<BoundMemberAccessExpression>(invocation.Arguments.Single().Expression);
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