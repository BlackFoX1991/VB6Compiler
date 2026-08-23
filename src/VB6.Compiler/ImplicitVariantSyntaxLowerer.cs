using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Compiler;

/// <summary>
/// Normalize VB6 declarations without an explicit <c>As ...</c> clause to Variant while leaving
/// identifier type suffixes intact for semantic binding.
/// </summary>
internal static class ImplicitVariantSyntaxLowerer
{
    public static CompilationUnitSyntax Lower(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var members = root.Members.Select(LowerMember).ToImmutableArray();
        return new CompilationUnitSyntax(members, root.EndOfFileToken);
    }

    private static MemberSyntax LowerMember(MemberSyntax member) => member switch
    {
        ModuleVariableDeclarationSyntax declaration => declaration with
        {
            Declarators = LowerDeclarators(declaration.Declarators)
        },
        SubDeclarationSyntax sub => sub with
        {
            Parameters = LowerParameters(sub.Parameters),
            Statements = LowerStatements(sub.Statements)
        },
        FunctionDeclarationSyntax function => LowerFunction(function),
        PropertyDeclarationSyntax property => property with
        {
            Parameters = LowerParameters(property.Parameters),
            Statements = LowerStatements(property.Statements)
        },
        _ => member
    };

    private static ImmutableArray<StatementSyntax> LowerStatements(ImmutableArray<StatementSyntax> statements) =>
        statements.Select(LowerStatement).ToImmutableArray();

    private static StatementSyntax LowerStatement(StatementSyntax statement) => statement switch
    {
        DimStatementSyntax dim => dim with
        {
            Declarators = LowerDeclarators(dim.Declarators)
        },
        StaticStatementSyntax staticStatement => staticStatement with
        {
            Declarators = LowerDeclarators(staticStatement.Declarators)
        },
        IfStatementSyntax ifStatement => ifStatement with
        {
            Statements = LowerStatements(ifStatement.Statements),
            ElseIfClauses = ifStatement.ElseIfClauses.Select(clause => clause with
            {
                Statements = LowerStatements(clause.Statements)
            }).ToImmutableArray(),
            ElseStatements = LowerStatements(ifStatement.ElseStatements)
        },
        ForStatementSyntax forStatement => forStatement with
        {
            Statements = LowerStatements(forStatement.Statements)
        },
        ForEachStatementSyntax forEach => forEach with
        {
            Statements = LowerStatements(forEach.Statements)
        },
        WhileStatementSyntax whileStatement => whileStatement with
        {
            Statements = LowerStatements(whileStatement.Statements)
        },
        DoStatementSyntax doStatement => doStatement with
        {
            Statements = LowerStatements(doStatement.Statements)
        },
        WithStatementSyntax withStatement => withStatement with
        {
            Statements = LowerStatements(withStatement.Statements)
        },
        SelectCaseStatementSyntax selectStatement => selectStatement with
        {
            Cases = selectStatement.Cases.Select(caseBlock => caseBlock with
            {
                Statements = LowerStatements(caseBlock.Statements)
            }).ToImmutableArray()
        },
        _ => statement
    };

    /// <summary>
    /// A Function without an As clause returns Variant. Filling the clause in here keeps the rule
    /// in one place instead of teaching the binder a second defaulting path.
    /// </summary>
    private static FunctionDeclarationSyntax LowerFunction(FunctionDeclarationSyntax function)
    {
        var lowered = function with
        {
            Parameters = LowerParameters(function.Parameters),
            Statements = LowerStatements(function.Statements)
        };
        if (lowered.ReturnTypeToken is not null || lowered.Identifier.TypeSuffix is not null)
        {
            return lowered;
        }

        var position = lowered.CloseParenthesisToken.Span.End;
        return lowered with
        {
            AsKeyword = SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            ReturnTypeToken = SyntheticToken(SyntaxKind.IdentifierToken, "Variant", position)
        };
    }

    private static ImmutableArray<VariableDeclaratorSyntax> LowerDeclarators(
        ImmutableArray<VariableDeclaratorSyntax> declarators) =>
        declarators.Select(LowerDeclarator).ToImmutableArray();

    private static ImmutableArray<ParameterSyntax> LowerParameters(
        ImmutableArray<ParameterSyntax> parameters) =>
        parameters.Select(LowerParameter).ToImmutableArray();

    private static ParameterSyntax LowerParameter(ParameterSyntax parameter)
    {
        if (parameter.TypeToken is not null || parameter.Identifier.TypeSuffix is not null)
        {
            return parameter;
        }

        var position = parameter.Identifier.Span.End;
        return parameter with
        {
            AsKeyword = SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            TypeToken = SyntheticToken(SyntaxKind.IdentifierToken, "Variant", position)
        };
    }

    private static VariableDeclaratorSyntax LowerDeclarator(VariableDeclaratorSyntax declarator)
    {
        if (declarator.TypeToken is not null || declarator.Identifier.TypeSuffix is not null)
        {
            return declarator;
        }

        var position = declarator.Identifier.Span.End;
        return declarator with
        {
            AsKeyword = SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            TypeToken = SyntheticToken(SyntaxKind.IdentifierToken, "Variant", position)
        };
    }

    private static SyntaxToken SyntheticToken(SyntaxKind kind, string text, int position) =>
        new(kind, new TextSpan(position, 0), text, null, ImmutableArray<SyntaxTrivia>.Empty);
}
