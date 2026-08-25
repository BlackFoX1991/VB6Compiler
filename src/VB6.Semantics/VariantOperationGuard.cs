using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;

namespace VB6.Semantics;

/// <summary>
/// Variant storage and the currently implemented Variant operator matrix are supported before the
/// final VB6 promotion rewrite. This guard keeps unsupported Variant constructs from falling into
/// scalar IR lowering. String concatenation with <c>&amp;</c> routes through
/// <c>VBOperators.Concat</c>/<c>CStr</c>.
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

            case BoundDebugAssertStatement debugAssert:
                VisitExpression(text, debugAssert.Expression, diagnostics);
                break;

            case BoundInvocationStatement invocation:
                foreach (var argument in invocation.Arguments)
                {
                    VisitExpression(text, argument.Expression, diagnostics);
                }
                break;

            case BoundRaiseEventStatement raiseEvent:
                foreach (var argument in raiseEvent.Arguments)
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
                var isSupportedVariantUnary =
                    (unary.Operand.Type == TypeSymbol.Variant &&
                     unary.OperatorKind is SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.NotKeyword) ||
                    (unary.OperatorKind == SyntaxKind.NotKeyword && unary.ResultType == TypeSymbol.Boolean);
                if (ContainsVariantValue(unary.Operand) && !isSupportedVariantUnary)
                {
                    AddOperatorDiagnostic(
                        text,
                        unary.OperatorKind.ToString(),
                        $"operand={DescribeExpression(unary.Operand)}",
                        diagnostics);
                }
                VisitExpression(text, unary.Operand, diagnostics);
                break;

            case BoundBinaryExpression binary:
            {
                var hasVariantOperand =
                    ContainsVariantValue(binary.Left) || ContainsVariantValue(binary.Right);
                var isVariantComparison = (binary.Type == TypeSymbol.Boolean || binary.Type == TypeSymbol.Variant) &&
                    (binary.OperatorKind is SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken or
                        SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or
                        SyntaxKind.GreaterOrEqualsToken) &&
                    (binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant);
                var isVariantLike = binary.OperatorKind == SyntaxKind.LikeKeyword &&
                    binary.Type == TypeSymbol.Boolean;
                var isVariantObjectIdentity = binary.OperatorKind == SyntaxKind.IsKeyword &&
                    binary.Type == TypeSymbol.Boolean;
                var isBooleanLogicalOperation = binary.Type == TypeSymbol.Boolean &&
                    binary.OperatorKind is SyntaxKind.AndKeyword or SyntaxKind.OrKeyword or
                        SyntaxKind.XorKeyword or SyntaxKind.EqvKeyword or SyntaxKind.ImpKeyword;
                var isSupportedVariantOperation =
                    (binary.Type == TypeSymbol.Variant && binary.OperatorKind is
                        SyntaxKind.CaretToken or SyntaxKind.PlusToken or SyntaxKind.MinusToken or
                        SyntaxKind.StarToken or SyntaxKind.SlashToken or SyntaxKind.BackslashToken or
                        SyntaxKind.ModKeyword or SyntaxKind.AndKeyword or SyntaxKind.OrKeyword or
                        SyntaxKind.XorKeyword or SyntaxKind.EqvKeyword or SyntaxKind.ImpKeyword) ||
                    isVariantComparison || isVariantLike || isVariantObjectIdentity || isBooleanLogicalOperation;
                var isBoundStringConcatenation =
                    binary.OperatorKind == SyntaxKind.AmpersandToken &&
                    binary.Type == TypeSymbol.String &&
                    (binary.Left.Type == TypeSymbol.String || binary.Left.Type == TypeSymbol.Variant) &&
                    (binary.Right.Type == TypeSymbol.String || binary.Right.Type == TypeSymbol.Variant);
                if (hasVariantOperand &&
                    !isSupportedVariantOperation &&
                    !isBoundStringConcatenation)
                {
                    AddOperatorDiagnostic(
                        text,
                        binary.OperatorKind.ToString(),
                        $"left={DescribeExpression(binary.Left)}, right={DescribeExpression(binary.Right)}, result={binary.Type.Name}",
                        diagnostics);
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

            case BoundVariantArrayAccessExpression variantArrayAccess:
                VisitExpression(text, variantArrayAccess.Receiver, diagnostics);
                foreach (var index in variantArrayAccess.Indices)
                {
                    VisitExpression(text, index, diagnostics);
                }
                break;

            case BoundArrayBoundExpression arrayBound:
                VisitExpression(text, arrayBound.Array, diagnostics);
                VisitExpression(text, arrayBound.Dimension, diagnostics);
                break;

            case BoundMemberAccessExpression memberAccess:
                VisitExpression(text, memberAccess.Receiver, diagnostics);
                break;

            case BoundPropertyAccessExpression propertyAccess:
                VisitExpression(text, propertyAccess.Receiver, diagnostics);
                break;

            case BoundPropertyInvocationExpression propertyInvocation:
                VisitExpression(text, propertyInvocation.Receiver, diagnostics);
                foreach (var argument in propertyInvocation.Arguments)
                {
                    VisitExpression(text, argument.Expression, diagnostics);
                }
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

    private static string DescribeExpression(BoundExpression expression)
    {
        var origin = ContainsVariantValue(expression) ? ",variant-origin" : string.Empty;
        return expression switch
        {
            BoundVariableExpression variable =>
                $"variable:{variable.Variable.Name}:{expression.Type.Name}{origin}",
            BoundInvocationExpression invocation =>
                $"call:{invocation.Procedure.Name}:{expression.Type.Name}{origin}",
            BoundConversionExpression conversion =>
                $"conversion:{conversion.Expression.Type.Name}->{conversion.TargetType.Name}{origin}",
            BoundLiteralExpression => $"literal:{expression.Type.Name}{origin}",
            _ => $"{expression.Kind}:{expression.Type.Name}{origin}"
        };
    }

    private static void AddOperatorDiagnostic(
        SourceText text,
        string operatorKind,
        string operandShape,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        diagnostics.Add(new Diagnostic(
            "VB6S0053",
            DiagnosticSeverity.Error,
            $"Variant operator '{operatorKind}' is not implemented yet ({operandShape}).",
            new TextSpan(0, 0),
            text.FilePath));
    }
}
