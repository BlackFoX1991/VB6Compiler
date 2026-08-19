using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;

namespace VB6.Semantics;

/// <summary>
/// Variant storage and explicit conversions are supported before the full VB6 Variant operator
/// promotion matrix. This guard prevents already-bound unary/binary expressions from being
/// lowered with scalar rules when any operand originates from a Variant value. Multiplication is
/// allowed only after VariantMultiplyLowerer has marked the bound result as Variant; the narrow
/// Variant-left integral equality slice is allowed only after the same lowerer has normalized both
/// operands to Double.
/// </summary>
public static class VariantOperationGuard
{
    public static ImmutableArray<Diagnostic> Validate(SourceText text, SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(model);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var variable in model.ModuleVariables)
        {
            if (variable.Initializer is not null)
            {
                VisitExpression(text, variable.Initializer, diagnostics);
            }
        }

        foreach (var procedure in model.Procedures)
        {
            VisitStatement(text, procedure.Body, diagnostics);
        }

        return diagnostics.ToImmutable();
    }

    private static void VisitStatement(
        SourceText text,
        BoundStatement statement,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        switch (statement)
        {
            case BoundBlockStatement block:
                foreach (var child in block.Statements)
                {
                    VisitStatement(text, child, diagnostics);
                }
                break;

            case BoundReDimStatement reDim:
                foreach (var dimension in reDim.ArrayDimensions)
                {
                    VisitExpression(text, dimension.LowerBound, diagnostics);
                    VisitExpression(text, dimension.UpperBound, diagnostics);
                }
                break;

            case BoundAssignmentStatement assignment:
                VisitExpression(text, assignment.Expression, diagnostics);
                break;

            case BoundArrayElementAssignmentStatement assignment:
                foreach (var index in assignment.Indices)
                {
                    VisitExpression(text, index, diagnostics);
                }
                VisitExpression(text, assignment.Expression, diagnostics);
                break;

            case BoundMemberAssignmentStatement assignment:
                VisitExpression(text, assignment.Target, diagnostics);
                VisitExpression(text, assignment.Expression, diagnostics);
                break;

            case BoundIfStatement ifStatement:
                VisitExpression(text, ifStatement.Condition, diagnostics);
                VisitStatement(text, ifStatement.Body, diagnostics);
                foreach (var clause in ifStatement.ElseIfClauses)
                {
                    VisitExpression(text, clause.Condition, diagnostics);
                    VisitStatement(text, clause.Body, diagnostics);
                }
                if (ifStatement.ElseBody is not null)
                {
                    VisitStatement(text, ifStatement.ElseBody, diagnostics);
                }
                break;

            case BoundForStatement forStatement:
                VisitExpression(text, forStatement.InitialValue, diagnostics);
                VisitExpression(text, forStatement.Limit, diagnostics);
                VisitExpression(text, forStatement.Step, diagnostics);
                VisitStatement(text, forStatement.Body, diagnostics);
                break;

            case BoundForEachStatement forEachStatement:
                VisitExpression(text, forEachStatement.Collection, diagnostics);
                VisitStatement(text, forEachStatement.Body, diagnostics);
                break;

            case BoundWhileStatement whileStatement:
                VisitExpression(text, whileStatement.Condition, diagnostics);
                VisitStatement(text, whileStatement.Body, diagnostics);
                break;

            case BoundDoStatement doStatement:
                if (doStatement.Condition is not null)
                {
                    VisitExpression(text, doStatement.Condition, diagnostics);
                }
                VisitStatement(text, doStatement.Body, diagnostics);
                break;

            case BoundWithStatement withStatement:
                VisitExpression(text, withStatement.Target, diagnostics);
                VisitStatement(text, withStatement.Body, diagnostics);
                break;

            case BoundSelectCaseStatement selectStatement:
                VisitExpression(text, selectStatement.Expression, diagnostics);
                foreach (var caseBlock in selectStatement.Cases)
                {
                    foreach (var clause in caseBlock.Clauses)
                    {
                        switch (clause)
                        {
                            case BoundCaseValueClause value:
                                VisitExpression(text, value.Value, diagnostics);
                                break;
                            case BoundCaseRangeClause range:
                                VisitExpression(text, range.LowerBound, diagnostics);
                                VisitExpression(text, range.UpperBound, diagnostics);
                                break;
                            case BoundCaseRelationalClause relational:
                                VisitExpression(text, relational.Value, diagnostics);
                                break;
                        }
                    }
                    VisitStatement(text, caseBlock.Body, diagnostics);
                }
                break;

            case BoundDebugPrintStatement debugPrint:
                VisitExpression(text, debugPrint.Expression, diagnostics);
                break;

            case BoundInvocationStatement invocation:
                foreach (var argument in invocation.Arguments)
                {
                    VisitExpression(text, argument.Expression, diagnostics);
                }
                break;
        }
    }

    private static void VisitExpression(
        SourceText text,
        BoundExpression expression,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        switch (expression)
        {
            case BoundUnaryExpression unary:
                if (ContainsVariantValue(unary.Operand))
                {
                    AddOperatorDiagnostic(text, unary.OperatorKind.ToString(), diagnostics);
                }
                VisitExpression(text, unary.Operand, diagnostics);
                break;

            case BoundBinaryExpression binary:
            {
                var hasVariantOperand =
                    ContainsVariantValue(binary.Left) || ContainsVariantValue(binary.Right);
                var isLoweredMultiply =
                    binary.OperatorKind == SyntaxKind.StarToken && binary.Type == TypeSymbol.Variant;
                var isLoweredIntegralEquality =
                    VariantMultiplyLowerer.IsLoweredVariantIntegralEquality(binary);
                if (hasVariantOperand && !isLoweredMultiply && !isLoweredIntegralEquality)
                {
                    AddOperatorDiagnostic(text, binary.OperatorKind.ToString(), diagnostics);
                }
                VisitExpression(text, binary.Left, diagnostics);
                VisitExpression(text, binary.Right, diagnostics);
                break;
            }

            case BoundConversionExpression conversion:
                VisitExpression(text, conversion.Expression, diagnostics);
                break;

            case BoundInvocationExpression invocation:
                foreach (var argument in invocation.Arguments)
                {
                    VisitExpression(text, argument.Expression, diagnostics);
                }
                break;

            case BoundArrayAccessExpression arrayAccess:
                foreach (var index in arrayAccess.Indices)
                {
                    VisitExpression(text, index, diagnostics);
                }
                break;

            case BoundElementAccessExpression elementAccess:
                VisitExpression(text, elementAccess.Receiver, diagnostics);
                foreach (var index in elementAccess.Indices)
                {
                    VisitExpression(text, index, diagnostics);
                }
                break;

            case BoundArrayBoundExpression arrayBound:
                VisitExpression(text, arrayBound.Dimension, diagnostics);
                break;

            case BoundMemberAccessExpression memberAccess:
                VisitExpression(text, memberAccess.Receiver, diagnostics);
                break;
        }
    }

    private static bool ContainsVariantValue(BoundExpression expression)
    {
        if (expression.Type == TypeSymbol.Variant)
        {
            return true;
        }

        return expression switch
        {
            BoundConversionExpression conversion => ContainsVariantValue(conversion.Expression),
            BoundUnaryExpression unary => ContainsVariantValue(unary.Operand),
            BoundBinaryExpression binary =>
                ContainsVariantValue(binary.Left) || ContainsVariantValue(binary.Right),
            _ => false
        };
    }

    private static void AddOperatorDiagnostic(
        SourceText text,
        string operatorKind,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        diagnostics.Add(new Diagnostic(
            "VB6S0053",
            DiagnosticSeverity.Error,
            $"Variant operator '{operatorKind}' is not implemented yet.",
            new TextSpan(0, 0),
            text.FilePath));
    }
}
