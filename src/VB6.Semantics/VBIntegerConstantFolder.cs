using VB6.Syntax;
using VB6.Syntax.Nodes;

namespace VB6.Semantics;

/// <summary>
/// Folds a compile-time integer expression. VB6 accepts a named constant and constant arithmetic
/// wherever a declaration needs a fixed number — an array bound of a user-defined type member, and
/// the width of a <c>String * n</c>.
///
/// Both of those live in different binders, and each used to carry its own, weaker check. Keeping
/// one folder is not tidiness: a width that folds in a UDT member but not in a Dim would make the
/// same source mean two different things depending on where it is written.
/// </summary>
internal static class VBIntegerConstantFolder
{
    /// <summary>
    /// Reads the module-level <c>Const</c> declarations into a lookup. It repeats until nothing
    /// new resolves, because a constant may refer to another one declared further down.
    /// </summary>
    public static Dictionary<string, long> CollectIntegerConstants(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var constants = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var declarations = root.Members.OfType<ConstDeclarationSyntax>().ToArray();

        for (var round = 0; round < declarations.Length; round++)
        {
            var added = false;
            foreach (var declaration in declarations)
            {
                if (constants.ContainsKey(declaration.Identifier.Text) ||
                    !TryEvaluate(declaration.Value, constants, out var value))
                {
                    continue;
                }

                constants[declaration.Identifier.Text] = value;
                added = true;
            }

            if (!added)
            {
                break;
            }
        }

        return constants;
    }

    /// <summary>
    /// Overflow counts as "does not fold" rather than as its own error: the caller reports it at
    /// the use site, where the source position is meaningful.
    /// </summary>
    public static bool TryEvaluate(
        ExpressionSyntax expression,
        IReadOnlyDictionary<string, long> constants,
        out long value)
    {
        ArgumentNullException.ThrowIfNull(constants);
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.LiteralToken.Kind == SyntaxKind.IntegerLiteralToken:
                value = Convert.ToInt64(
                    literal.LiteralToken.Value,
                    System.Globalization.CultureInfo.InvariantCulture);
                return true;

            case ParenthesizedExpressionSyntax parenthesized:
                return TryEvaluate(parenthesized.Expression, constants, out value);

            case NameExpressionSyntax name:
                return constants.TryGetValue(name.IdentifierToken.Text, out value);

            case UnaryExpressionSyntax unary when unary.OperatorToken.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken:
                if (TryEvaluate(unary.Operand, constants, out var operand))
                {
                    try
                    {
                        value = unary.OperatorToken.Kind == SyntaxKind.MinusToken
                            ? checked(-operand)
                            : operand;
                        return true;
                    }
                    catch (OverflowException)
                    {
                        break;
                    }
                }

                break;

            case BinaryExpressionSyntax binary:
                if (TryEvaluate(binary.Left, constants, out var left) &&
                    TryEvaluate(binary.Right, constants, out var right))
                {
                    return TryApplyIntegerOperator(binary.OperatorToken.Kind, left, right, out value);
                }

                break;
        }

        value = 0;
        return false;
    }

    private static bool TryApplyIntegerOperator(SyntaxKind operatorKind, long left, long right, out long value)
    {
        try
        {
            switch (operatorKind)
            {
                case SyntaxKind.PlusToken:
                    value = checked(left + right);
                    return true;
                case SyntaxKind.MinusToken:
                    value = checked(left - right);
                    return true;
                case SyntaxKind.StarToken:
                    value = checked(left * right);
                    return true;
                case SyntaxKind.BackslashToken when right != 0:
                    value = left / right;
                    return true;
            }
        }
        catch (OverflowException)
        {
            // Treated as "does not fold"; the message belongs to the use site.
        }

        value = 0;
        return false;
    }
}
