using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Compiler;

/// <summary>
/// Normalize VB6 declarations without an explicit <c>As ...</c> clause to their module-level
/// implicit default type, or to Variant when no <c>DefType</c> directive covers the identifier.
/// Identifier type suffixes and explicit <c>As</c> clauses always take precedence.
/// </summary>
internal static class ImplicitVariantSyntaxLowerer
{
    public static CompilationUnitSyntax Lower(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var defaults = DefaultTypeMap.Create(root);
        var members = root.Members.Select(member => LowerMember(member, defaults)).ToImmutableArray();
        return new CompilationUnitSyntax(members, root.EndOfFileToken);
    }

    private static MemberSyntax LowerMember(MemberSyntax member, DefaultTypeMap defaults) => member switch
    {
        ModuleVariableDeclarationSyntax declaration => declaration with
        {
            Declarators = LowerDeclarators(declaration.Declarators, defaults)
        },
        SubDeclarationSyntax sub => sub with
        {
            Parameters = LowerParameters(sub.Parameters, defaults),
            Statements = LowerStatements(sub.Statements, defaults)
        },
        FunctionDeclarationSyntax function => LowerFunction(function, defaults),
        PropertyDeclarationSyntax property => LowerProperty(property, defaults),
        _ => member
    };

    private static ImmutableArray<StatementSyntax> LowerStatements(
        ImmutableArray<StatementSyntax> statements,
        DefaultTypeMap defaults) =>
        statements.Select(statement => LowerStatement(statement, defaults)).ToImmutableArray();

    private static StatementSyntax LowerStatement(StatementSyntax statement, DefaultTypeMap defaults) => statement switch
    {
        DimStatementSyntax dim => dim with
        {
            Declarators = LowerDeclarators(dim.Declarators, defaults)
        },
        StaticStatementSyntax staticStatement => staticStatement with
        {
            Declarators = LowerDeclarators(staticStatement.Declarators, defaults)
        },
        IfStatementSyntax ifStatement => ifStatement with
        {
            Statements = LowerStatements(ifStatement.Statements, defaults),
            ElseIfClauses = ifStatement.ElseIfClauses.Select(clause => clause with
            {
                Statements = LowerStatements(clause.Statements, defaults)
            }).ToImmutableArray(),
            ElseStatements = LowerStatements(ifStatement.ElseStatements, defaults)
        },
        ForStatementSyntax forStatement => forStatement with
        {
            Statements = LowerStatements(forStatement.Statements, defaults)
        },
        ForEachStatementSyntax forEach => forEach with
        {
            Statements = LowerStatements(forEach.Statements, defaults)
        },
        WhileStatementSyntax whileStatement => whileStatement with
        {
            Statements = LowerStatements(whileStatement.Statements, defaults)
        },
        DoStatementSyntax doStatement => doStatement with
        {
            Statements = LowerStatements(doStatement.Statements, defaults)
        },
        WithStatementSyntax withStatement => withStatement with
        {
            Statements = LowerStatements(withStatement.Statements, defaults)
        },
        SelectCaseStatementSyntax selectStatement => selectStatement with
        {
            Cases = selectStatement.Cases.Select(caseBlock => caseBlock with
            {
                Statements = LowerStatements(caseBlock.Statements, defaults)
            }).ToImmutableArray()
        },
        _ => statement
    };

    /// <summary>
    /// A Function without an As clause returns the module default type for its first letter, or
    /// Variant. Filling the clause in here keeps the rule in one place instead of teaching the
    /// binder a second defaulting path.
    /// </summary>
    private static FunctionDeclarationSyntax LowerFunction(
        FunctionDeclarationSyntax function,
        DefaultTypeMap defaults)
    {
        var lowered = function with
        {
            Parameters = LowerParameters(function.Parameters, defaults),
            Statements = LowerStatements(function.Statements, defaults)
        };
        if (lowered.ReturnTypeToken is not null || lowered.Identifier.TypeSuffix is not null)
        {
            return lowered;
        }

        var position = lowered.CloseParenthesisToken.Span.End;
        return lowered with
        {
            AsKeyword = SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            ReturnTypeToken = SyntheticToken(
                SyntaxKind.IdentifierToken,
                defaults.GetTypeName(lowered.Identifier),
                position)
        };
    }

    private static PropertyDeclarationSyntax LowerProperty(
        PropertyDeclarationSyntax property,
        DefaultTypeMap defaults)
    {
        var lowered = property with
        {
            Parameters = LowerParameters(property.Parameters, defaults),
            Statements = LowerStatements(property.Statements, defaults)
        };
        if (!lowered.IsGet || lowered.ReturnTypeToken is not null || lowered.Identifier.TypeSuffix is not null)
        {
            return lowered;
        }

        var position = lowered.CloseParenthesisToken.Span.End;
        return lowered with
        {
            AsKeyword = SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            ReturnTypeToken = SyntheticToken(
                SyntaxKind.IdentifierToken,
                defaults.GetTypeName(lowered.Identifier),
                position)
        };
    }

    private static ImmutableArray<VariableDeclaratorSyntax> LowerDeclarators(
        ImmutableArray<VariableDeclaratorSyntax> declarators,
        DefaultTypeMap defaults) =>
        declarators.Select(declarator => LowerDeclarator(declarator, defaults)).ToImmutableArray();

    private static ImmutableArray<ParameterSyntax> LowerParameters(
        ImmutableArray<ParameterSyntax> parameters,
        DefaultTypeMap defaults) =>
        parameters.Select(parameter => LowerParameter(parameter, defaults)).ToImmutableArray();

    private static ParameterSyntax LowerParameter(ParameterSyntax parameter, DefaultTypeMap defaults)
    {
        if (parameter.TypeToken is not null || parameter.Identifier.TypeSuffix is not null)
        {
            return parameter;
        }

        var position = parameter.Identifier.Span.End;
        return parameter with
        {
            AsKeyword = SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            TypeToken = SyntheticToken(
                SyntaxKind.IdentifierToken,
                defaults.GetTypeName(parameter.Identifier),
                position)
        };
    }

    private static VariableDeclaratorSyntax LowerDeclarator(
        VariableDeclaratorSyntax declarator,
        DefaultTypeMap defaults)
    {
        if (declarator.TypeToken is not null || declarator.Identifier.TypeSuffix is not null)
        {
            return declarator;
        }

        var position = declarator.Identifier.Span.End;
        return declarator with
        {
            AsKeyword = SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            TypeToken = SyntheticToken(
                SyntaxKind.IdentifierToken,
                defaults.GetTypeName(declarator.Identifier),
                position)
        };
    }

    private static SyntaxToken SyntheticToken(SyntaxKind kind, string text, int position) =>
        new(kind, new TextSpan(position, 0), text, null, ImmutableArray<SyntaxTrivia>.Empty);

    private sealed class DefaultTypeMap
    {
        private readonly string?[] _types = new string?[26];

        public static DefaultTypeMap Create(CompilationUnitSyntax root)
        {
            var map = new DefaultTypeMap();
            foreach (var directive in root.Members.OfType<DefaultTypeStatementSyntax>())
            {
                var typeName = directive.DirectiveToken.Text.ToUpperInvariant() switch
                {
                    "DEFBOOL" => "Boolean",
                    "DEFBYTE" => "Byte",
                    "DEFCUR" => "Currency",
                    "DEFDATE" => "Date",
                    "DEFDBL" => "Double",
                    "DEFINT" => "Integer",
                    "DEFLNG" => "Long",
                    "DEFOBJ" => "Object",
                    "DEFSNG" => "Single",
                    "DEFSTR" => "String",
                    "DEFVAR" => "Variant",
                    _ => "Variant"
                };

                foreach (var range in directive.Ranges)
                {
                    var first = ToIndex(range.FirstLetter);
                    var last = range.LastLetter is null ? first : ToIndex(range.LastLetter);
                    if (first is null || last is null)
                    {
                        continue;
                    }

                    var lower = Math.Min(first.Value, last.Value);
                    var upper = Math.Max(first.Value, last.Value);
                    for (var index = lower; index <= upper; index++)
                    {
                        map._types[index] = typeName;
                    }
                }
            }

            return map;
        }

        public string GetTypeName(SyntaxToken identifier)
        {
            if (identifier.Text.Length == 0)
            {
                return "Variant";
            }

            var index = ToIndex(identifier.Text[0]);
            return index is not null ? _types[index.Value] ?? "Variant" : "Variant";
        }

        private static int? ToIndex(SyntaxToken token) =>
            token.Text.Length == 1 ? ToIndex(token.Text[0]) : null;

        private static int? ToIndex(char value)
        {
            var upper = char.ToUpperInvariant(value);
            return upper is >= 'A' and <= 'Z' ? upper - 'A' : null;
        }
    }
}
