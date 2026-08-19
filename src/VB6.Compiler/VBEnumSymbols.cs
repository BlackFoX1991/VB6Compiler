using System.Collections.Immutable;
using System.Globalization;
using VB6.Semantics;
using VB6.Syntax;
using VB6.Syntax.Nodes;

namespace VB6.Compiler;

/// <summary>
/// Projects the currently reachable VB6 Enum surface onto the compiler's existing Long backend.
/// VB6 Enum storage is Long-sized; preserving the named type as a scoped Long alias lets all
/// existing conversion/operator/codegen paths work without introducing a second numeric backend.
/// Enum members are exposed as immutable module-level Long constants.
/// </summary>
internal static class VBEnumSymbols
{
    public static VBEnumSymbolSet Bind(IEnumerable<CompilationUnitSyntax> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        var aliases = ImmutableDictionary.CreateBuilder<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        var constants = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        var values = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var declaredMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var declaration in root.Members.OfType<EnumDeclarationSyntax>())
            {
                aliases.TryAdd(declaration.Identifier.Text, TypeSymbol.Long);

                long nextValue = 0;
                foreach (var member in declaration.Members)
                {
                    long value;
                    if (member.Value is null)
                    {
                        value = nextValue;
                    }
                    else if (!TryEvaluate(member.Value, values, out value))
                    {
                        // Keep unsupported constant-expression forms explicit by leaving the member
                        // unbound instead of silently assigning an incorrect value.
                        continue;
                    }

                    values[member.Identifier.Text] = value;
                    nextValue = checked(value + 1);

                    if (!declaredMembers.Add(member.Identifier.Text))
                    {
                        continue;
                    }

                    var symbol = new ModuleVariableSymbol(member.Identifier.Text, TypeSymbol.Long);
                    constants.Add(new BoundModuleVariable(
                        symbol,
                        new BoundLiteralExpression(value, TypeSymbol.Long),
                        IsConstant: true));
                }
            }
        }

        return new VBEnumSymbolSet(aliases.ToImmutable(), constants.ToImmutable());
    }

    private static bool TryEvaluate(
        ExpressionSyntax expression,
        IReadOnlyDictionary<string, long> values,
        out long value)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.LiteralToken.Kind == SyntaxKind.IntegerLiteralToken:
                value = Convert.ToInt64(literal.LiteralToken.Value, CultureInfo.InvariantCulture);
                return true;

            case NameExpressionSyntax name when values.TryGetValue(name.IdentifierToken.Text, out value):
                return true;

            case ParenthesizedExpressionSyntax parenthesized:
                return TryEvaluate(parenthesized.Expression, values, out value);

            case UnaryExpressionSyntax unary when unary.OperatorToken.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken:
                if (TryEvaluate(unary.Operand, values, out var operand))
                {
                    value = unary.OperatorToken.Kind == SyntaxKind.MinusToken
                        ? checked(-operand)
                        : operand;
                    return true;
                }
                break;

            case BinaryExpressionSyntax binary when binary.OperatorToken.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken:
                if (TryEvaluate(binary.Left, values, out var left) &&
                    TryEvaluate(binary.Right, values, out var right))
                {
                    value = binary.OperatorToken.Kind == SyntaxKind.PlusToken
                        ? checked(left + right)
                        : checked(left - right);
                    return true;
                }
                break;
        }

        value = 0;
        return false;
    }
}

internal sealed record VBEnumSymbolSet(
    ImmutableDictionary<string, TypeSymbol> TypeAliases,
    ImmutableArray<BoundModuleVariable> Constants)
{
    public ImmutableArray<BoundModuleVariable> AddMemberSymbols(
        IDictionary<string, ModuleVariableSymbol> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var visible = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        foreach (var constant in Constants)
        {
            if (variables.ContainsKey(constant.Symbol.Name))
            {
                continue;
            }

            variables.Add(constant.Symbol.Name, constant.Symbol);
            visible.Add(constant);
        }

        return visible.ToImmutable();
    }
}
