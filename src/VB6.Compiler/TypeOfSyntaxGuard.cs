using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Compiler;

internal static class TypeOfSyntaxGuard
{
    public static ImmutableArray<Diagnostic> Validate(SourceText text, CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var member in root.Members)
        {
            switch (member)
            {
                case SubDeclarationSyntax sub:
                    VisitStatements(text, sub.Statements, diagnostics);
                    break;
                case FunctionDeclarationSyntax function:
                    VisitStatements(text, function.Statements, diagnostics);
                    break;
            }
        }

        return diagnostics.ToImmutable();
    }

    private static void VisitStatements(
        SourceText text,
        ImmutableArray<StatementSyntax> statements,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case AssignmentStatementSyntax assignment:
                    VisitExpression(text, assignment.Expression, diagnostics);
                    break;
                case ArrayElementAssignmentStatementSyntax assignment:
                    foreach (var index in assignment.Indices)
                    {
                        VisitExpression(text, index, diagnostics);
                    }
                    VisitExpression(text, assignment.Expression, diagnostics);
                    break;
                case MemberAssignmentStatementSyntax assignment:
                    VisitExpression(text, assignment.Target, diagnostics);
                    VisitExpression(text, assignment.Expression, diagnostics);
                    break;
                case IfStatementSyntax ifStatement:
                    VisitExpression(text, ifStatement.Condition, diagnostics);
                    VisitStatements(text, ifStatement.Statements, diagnostics);
                    foreach (var clause in ifStatement.ElseIfClauses)
                    {
                        VisitExpression(text, clause.Condition, diagnostics);
                        VisitStatements(text, clause.Statements, diagnostics);
                    }
                    VisitStatements(text, ifStatement.ElseStatements, diagnostics);
                    break;
                case ForStatementSyntax forStatement:
                    VisitExpression(text, forStatement.InitialValue, diagnostics);
                    VisitExpression(text, forStatement.Limit, diagnostics);
                    if (forStatement.Step is not null)
                    {
                        VisitExpression(text, forStatement.Step, diagnostics);
                    }
                    VisitStatements(text, forStatement.Statements, diagnostics);
                    break;
                case ForEachStatementSyntax forEachStatement:
                    VisitExpression(text, forEachStatement.Collection, diagnostics);
                    VisitStatements(text, forEachStatement.Statements, diagnostics);
                    break;
                case WhileStatementSyntax whileStatement:
                    VisitExpression(text, whileStatement.Condition, diagnostics);
                    VisitStatements(text, whileStatement.Statements, diagnostics);
                    break;
                case DoStatementSyntax doStatement:
                    if (doStatement.PreCondition is not null)
                    {
                        VisitExpression(text, doStatement.PreCondition, diagnostics);
                    }
                    if (doStatement.PostCondition is not null)
                    {
                        VisitExpression(text, doStatement.PostCondition, diagnostics);
                    }
                    VisitStatements(text, doStatement.Statements, diagnostics);
                    break;
                case WithStatementSyntax withStatement:
                    VisitExpression(text, withStatement.Expression, diagnostics);
                    VisitStatements(text, withStatement.Statements, diagnostics);
                    break;
                case SelectCaseStatementSyntax selectStatement:
                    VisitExpression(text, selectStatement.Expression, diagnostics);
                    foreach (var caseBlock in selectStatement.Cases)
                    {
                        foreach (var clause in caseBlock.Clauses)
                        {
                            switch (clause)
                            {
                                case CaseValueClauseSyntax value:
                                    VisitExpression(text, value.Value, diagnostics);
                                    break;
                                case CaseRangeClauseSyntax range:
                                    VisitExpression(text, range.LowerBound, diagnostics);
                                    VisitExpression(text, range.UpperBound, diagnostics);
                                    break;
                                case CaseRelationalClauseSyntax relational:
                                    VisitExpression(text, relational.Value, diagnostics);
                                    break;
                            }
                        }
                        VisitStatements(text, caseBlock.Statements, diagnostics);
                    }
                    break;
                case DebugPrintStatementSyntax debugPrint:
                    VisitExpression(text, debugPrint.Expression, diagnostics);
                    break;
                case InvocationStatementSyntax invocation:
                    foreach (var argument in invocation.Arguments)
                    {
                        VisitExpression(text, argument, diagnostics);
                    }
                    break;
            }
        }
    }

    private static void VisitExpression(
        SourceText text,
        ExpressionSyntax expression,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        switch (expression)
        {
            case TypeOfExpressionSyntax typeOf:
                diagnostics.Add(new Diagnostic(
                    "VB6S0058",
                    DiagnosticSeverity.Error,
                    $"VB6 TypeOf object check against '{typeOf.TypeName.Text}' is parsed but object type semantics are not implemented yet.",
                    typeOf.TypeOfToken.Span,
                    text.FilePath));
                VisitExpression(text, typeOf.Expression, diagnostics);
                break;
            case UnaryExpressionSyntax unary:
                VisitExpression(text, unary.Operand, diagnostics);
                break;
            case BinaryExpressionSyntax binary:
                VisitExpression(text, binary.Left, diagnostics);
                VisitExpression(text, binary.Right, diagnostics);
                break;
            case ParenthesizedExpressionSyntax parenthesized:
                VisitExpression(text, parenthesized.Expression, diagnostics);
                break;
            case InvocationExpressionSyntax invocation:
                foreach (var argument in invocation.Arguments)
                {
                    VisitExpression(text, argument, diagnostics);
                }
                break;
            case MemberAccessExpressionSyntax memberAccess:
                VisitExpression(text, memberAccess.Receiver, diagnostics);
                break;
            case ElementAccessExpressionSyntax elementAccess:
                VisitExpression(text, elementAccess.Receiver, diagnostics);
                foreach (var index in elementAccess.Indices)
                {
                    VisitExpression(text, index, diagnostics);
                }
                break;
        }
    }
}
