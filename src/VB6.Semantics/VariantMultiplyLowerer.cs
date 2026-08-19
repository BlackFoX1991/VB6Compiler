using System.Collections.Immutable;
using VB6.Syntax;

namespace VB6.Semantics;

/// <summary>
/// Corrects selected binder scalar fallbacks when a bound operand originates from Variant.
/// Multiplication is restored to Variant runtime dispatch. Equality currently supports the safe
/// VB6 subset of a Variant value on the left compared with a Byte/Integer/Long value on the right;
/// both sides are converted to Double so Empty, numeric strings, Boolean, and numeric Variant
/// subtypes use numeric comparison without narrowing the Variant to the scalar operand type.
/// Other Variant operators remain untouched and therefore stay behind VariantOperationGuard.
/// </summary>
public static class VariantMultiplyLowerer
{
    public static SemanticModel Lower(SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model with
        {
            ModuleVariables = model.ModuleVariables.Select(LowerModuleVariable).ToImmutableArray(),
            Procedures = model.Procedures.Select(LowerProcedure).ToImmutableArray()
        };
    }

    private static BoundModuleVariable LowerModuleVariable(BoundModuleVariable variable) =>
        variable with
        {
            Initializer = variable.Initializer is null
                ? null
                : LowerForTarget(variable.Initializer, variable.Symbol.Type),
            ArrayDimensions = LowerDimensions(variable.ArrayDimensions)
        };

    private static BoundProcedure LowerProcedure(BoundProcedure procedure) =>
        procedure with { Body = LowerBlock(procedure.Body) };

    private static BoundBlockStatement LowerBlock(BoundBlockStatement block) =>
        new(block.Statements.Select(LowerStatement).ToImmutableArray());

    private static BoundStatement LowerStatement(BoundStatement statement)
    {
        return statement switch
        {
            BoundVariableDeclarationStatement declaration => declaration with
            {
                ArrayDimensions = LowerDimensions(declaration.ArrayDimensions)
            },
            BoundReDimStatement reDim => reDim with
            {
                ArrayDimensions = LowerDimensions(reDim.ArrayDimensions)
            },
            BoundAssignmentStatement assignment => assignment with
            {
                Expression = LowerForTarget(assignment.Expression, assignment.Variable.Type)
            },
            BoundArrayElementAssignmentStatement assignment => assignment with
            {
                Indices = assignment.Indices.Select(index => LowerForTarget(index, TypeSymbol.Long)).ToImmutableArray(),
                Expression = LowerForTarget(
                    assignment.Expression,
                    ((ArrayTypeSymbol)assignment.Array.Type).ElementType)
            },
            BoundMemberAssignmentStatement assignment => LowerMemberAssignment(assignment),
            BoundIfStatement ifStatement => ifStatement with
            {
                Condition = LowerForTarget(ifStatement.Condition, TypeSymbol.Boolean),
                Body = LowerBlock(ifStatement.Body),
                ElseIfClauses = ifStatement.ElseIfClauses.Select(clause => clause with
                {
                    Condition = LowerForTarget(clause.Condition, TypeSymbol.Boolean),
                    Body = LowerBlock(clause.Body)
                }).ToImmutableArray(),
                ElseBody = ifStatement.ElseBody is null ? null : LowerBlock(ifStatement.ElseBody)
            },
            BoundForStatement forStatement => forStatement with
            {
                InitialValue = LowerForTarget(forStatement.InitialValue, forStatement.ControlVariable.Type),
                Limit = LowerForTarget(forStatement.Limit, forStatement.ControlVariable.Type),
                Step = LowerForTarget(forStatement.Step, forStatement.ControlVariable.Type),
                Body = LowerBlock(forStatement.Body)
            },
            BoundForEachStatement forEach => forEach with
            {
                Collection = LowerExpression(forEach.Collection),
                Body = LowerBlock(forEach.Body)
            },
            BoundWhileStatement whileStatement => whileStatement with
            {
                Condition = LowerForTarget(whileStatement.Condition, TypeSymbol.Boolean),
                Body = LowerBlock(whileStatement.Body)
            },
            BoundDoStatement doStatement => doStatement with
            {
                Condition = doStatement.Condition is null
                    ? null
                    : LowerForTarget(doStatement.Condition, TypeSymbol.Boolean),
                Body = LowerBlock(doStatement.Body)
            },
            BoundWithStatement withStatement => withStatement with
            {
                Target = LowerExpression(withStatement.Target),
                Body = LowerBlock(withStatement.Body)
            },
            BoundSelectCaseStatement selectStatement => selectStatement with
            {
                Expression = LowerExpression(selectStatement.Expression),
                Cases = selectStatement.Cases.Select(LowerCaseBlock).ToImmutableArray()
            },
            BoundDebugPrintStatement debugPrint => debugPrint with
            {
                Expression = LowerExpression(debugPrint.Expression)
            },
            BoundInvocationStatement invocation => invocation with
            {
                Arguments = invocation.Arguments.Select(LowerArgument).ToImmutableArray()
            },
            _ => statement
        };
    }

    private static BoundMemberAssignmentStatement LowerMemberAssignment(BoundMemberAssignmentStatement assignment)
    {
        var target = LowerExpression(assignment.Target);
        return assignment with
        {
            Target = target,
            Expression = LowerForTarget(assignment.Expression, target.Type)
        };
    }

    private static BoundCaseBlock LowerCaseBlock(BoundCaseBlock block) =>
        block with
        {
            Clauses = block.Clauses.Select(LowerCaseClause).ToImmutableArray(),
            Body = LowerBlock(block.Body)
        };

    private static BoundCaseClause LowerCaseClause(BoundCaseClause clause) => clause switch
    {
        BoundCaseValueClause value => value with { Value = LowerExpression(value.Value) },
        BoundCaseRangeClause range => range with
        {
            LowerBound = LowerExpression(range.LowerBound),
            UpperBound = LowerExpression(range.UpperBound)
        },
        BoundCaseRelationalClause relational => relational with
        {
            Value = LowerExpression(relational.Value)
        },
        _ => clause
    };

    private static BoundArgument LowerArgument(BoundArgument argument)
    {
        if (argument.Parameter is null || argument.Parameter.PassingMode == ParameterPassingMode.ByRef)
        {
            return argument with { Expression = LowerExpression(argument.Expression) };
        }

        return argument with
        {
            Expression = LowerForTarget(argument.Expression, argument.Parameter.Type)
        };
    }

    private static ImmutableArray<BoundArrayDimension> LowerDimensions(
        ImmutableArray<BoundArrayDimension> dimensions) =>
        dimensions.Select(dimension => new BoundArrayDimension(
            LowerForTarget(dimension.LowerBound, TypeSymbol.Long),
            LowerForTarget(dimension.UpperBound, TypeSymbol.Long))).ToImmutableArray();

    private static BoundExpression LowerForTarget(BoundExpression expression, TypeSymbol targetType)
    {
        var lowered = LowerExpression(expression);
        if (lowered.Type == TypeSymbol.Error || targetType == TypeSymbol.Error || lowered.Type == targetType)
        {
            return lowered;
        }

        return new BoundConversionExpression(targetType, lowered);
    }

    private static BoundExpression LowerExpression(BoundExpression expression)
    {
        switch (expression)
        {
            case BoundConversionExpression conversion:
            {
                var operand = LowerExpression(conversion.Expression);
                return operand.Type == conversion.TargetType
                    ? operand
                    : new BoundConversionExpression(conversion.TargetType, operand);
            }

            case BoundUnaryExpression unary:
                return unary with { Operand = LowerExpression(unary.Operand) };

            case BoundBinaryExpression binary:
            {
                var left = LowerExpression(binary.Left);
                var right = LowerExpression(binary.Right);
                if (binary.OperatorKind == SyntaxKind.StarToken &&
                    (OriginatesFromVariant(left) || OriginatesFromVariant(right)))
                {
                    return new BoundBinaryExpression(
                        StripBinderArithmeticConversion(left, binary.ResultType),
                        binary.OperatorKind,
                        StripBinderArithmeticConversion(right, binary.ResultType),
                        TypeSymbol.Variant);
                }

                if (TryLowerVariantIntegralEquality(binary, left, right, out var equality))
                {
                    return equality;
                }

                return binary with { Left = left, Right = right };
            }

            case BoundInvocationExpression invocation:
                return invocation with
                {
                    Arguments = invocation.Arguments.Select(LowerArgument).ToImmutableArray()
                };

            case BoundArrayAccessExpression arrayAccess:
                return arrayAccess with
                {
                    Indices = arrayAccess.Indices
                        .Select(index => LowerForTarget(index, TypeSymbol.Long))
                        .ToImmutableArray()
                };

            case BoundElementAccessExpression elementAccess:
                return elementAccess with
                {
                    Receiver = LowerExpression(elementAccess.Receiver),
                    Indices = elementAccess.Indices
                        .Select(index => LowerForTarget(index, TypeSymbol.Long))
                        .ToImmutableArray()
                };

            case BoundArrayBoundExpression arrayBound:
                return arrayBound with
                {
                    Dimension = LowerForTarget(arrayBound.Dimension, TypeSymbol.Long)
                };

            case BoundMemberAccessExpression memberAccess:
                return memberAccess with { Receiver = LowerExpression(memberAccess.Receiver) };

            default:
                return expression;
        }
    }

    private static bool TryLowerVariantIntegralEquality(
        BoundBinaryExpression original,
        BoundExpression left,
        BoundExpression right,
        out BoundExpression lowered)
    {
        lowered = original with { Left = left, Right = right };
        if (original.OperatorKind != SyntaxKind.EqualsToken || !IsRuntimeVariantValue(left))
        {
            return false;
        }

        if (right is not BoundConversionExpression
            {
                TargetType: var targetType,
                Expression: var scalar
            } || targetType != TypeSymbol.Variant || !IsSupportedEqualityScalar(scalar.Type))
        {
            return false;
        }

        lowered = new BoundBinaryExpression(
            new BoundConversionExpression(TypeSymbol.Double, left),
            SyntaxKind.EqualsToken,
            new BoundConversionExpression(TypeSymbol.Double, scalar),
            TypeSymbol.Boolean);
        return true;
    }

    private static bool IsRuntimeVariantValue(BoundExpression expression)
    {
        if (expression.Type != TypeSymbol.Variant)
        {
            return false;
        }

        return expression is not BoundConversionExpression
        {
            TargetType: var targetType,
            Expression: var operand
        } || targetType != TypeSymbol.Variant || operand.Type == TypeSymbol.Variant;
    }

    private static bool IsSupportedEqualityScalar(TypeSymbol type) =>
        type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long;

    internal static bool IsLoweredVariantIntegralEquality(BoundBinaryExpression binary)
    {
        if (binary.OperatorKind != SyntaxKind.EqualsToken || binary.Type != TypeSymbol.Boolean ||
            binary.Left is not BoundConversionExpression { TargetType: var leftTarget, Expression: var leftOperand } ||
            binary.Right is not BoundConversionExpression { TargetType: var rightTarget, Expression: var rightOperand })
        {
            return false;
        }

        return leftTarget == TypeSymbol.Double &&
            rightTarget == TypeSymbol.Double &&
            IsRuntimeVariantValue(leftOperand) &&
            IsSupportedEqualityScalar(rightOperand.Type);
    }

    private static BoundExpression StripBinderArithmeticConversion(
        BoundExpression expression,
        TypeSymbol originalResultType)
    {
        if (expression is BoundConversionExpression conversion &&
            conversion.TargetType == originalResultType)
        {
            return conversion.Expression;
        }

        return expression;
    }

    internal static bool OriginatesFromVariant(BoundExpression expression)
    {
        if (expression.Type == TypeSymbol.Variant)
        {
            return true;
        }

        return expression switch
        {
            BoundConversionExpression conversion => OriginatesFromVariant(conversion.Expression),
            BoundUnaryExpression unary => OriginatesFromVariant(unary.Operand),
            BoundBinaryExpression binary =>
                OriginatesFromVariant(binary.Left) || OriginatesFromVariant(binary.Right),
            _ => false
        };
    }
}
