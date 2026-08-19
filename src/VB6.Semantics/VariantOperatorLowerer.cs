using System.Collections.Immutable;
using VB6.Syntax;

namespace VB6.Semantics;

/// <summary>
/// Corrects scalar fallback binding for the Variant operators that are implemented by the managed
/// runtime. Multiplication restores Variant operands/result semantics. Equality currently restores
/// exactly one Variant operand against one statically typed scalar and retains a Boolean result;
/// Variant-to-Variant equality remains behind VariantOperationGuard.
/// </summary>
public static class VariantOperatorLowerer
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
                var leftFromVariant = OriginatesFromVariant(left);
                var rightFromVariant = OriginatesFromVariant(right);

                if (binary.OperatorKind == SyntaxKind.StarToken && (leftFromVariant || rightFromVariant))
                {
                    return new BoundBinaryExpression(
                        StripBinderArithmeticConversion(left, binary.ResultType),
                        binary.OperatorKind,
                        StripBinderArithmeticConversion(right, binary.ResultType),
                        TypeSymbol.Variant);
                }

                if (binary.OperatorKind == SyntaxKind.EqualsToken && (leftFromVariant || rightFromVariant))
                {
                    var restoredLeft = RestoreVariantComparisonOperand(left);
                    var restoredRight = RestoreVariantComparisonOperand(right);
                    var restoredLeftFromVariant = OriginatesFromVariant(restoredLeft);
                    var restoredRightFromVariant = OriginatesFromVariant(restoredRight);
                    if (restoredLeftFromVariant != restoredRightFromVariant)
                    {
                        return new BoundBinaryExpression(
                            restoredLeft,
                            binary.OperatorKind,
                            restoredRight,
                            TypeSymbol.Boolean);
                    }
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

    private static BoundExpression RestoreVariantComparisonOperand(BoundExpression expression)
    {
        if (expression is not BoundConversionExpression conversion)
        {
            return expression;
        }

        if (conversion.TargetType == TypeSymbol.Variant || OriginatesFromVariant(conversion.Expression))
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
