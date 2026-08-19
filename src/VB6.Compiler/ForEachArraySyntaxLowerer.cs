using System.Collections.Immutable;
using System.Globalization;
using VB6.Semantics;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Compiler;

internal sealed record ForEachArrayLoweringResult(
    CompilationUnitSyntax Root,
    ImmutableArray<Diagnostic> Diagnostics);

/// <summary>
/// Desugars supported VB6 array For Each loops into the compiler's already-tested numeric For and
/// array-indexing syntax. A preliminary semantic model supplies the declared control/collection
/// types and fixed array rank. The generated loops enumerate dimension 1 outermost and the
/// rightmost dimension innermost, matching VB6 array iteration order.
/// Dynamic/unknown-rank arrays are deliberately left in syntax form for direct semantic lowering.
/// </summary>
internal sealed class ForEachArraySyntaxLowerer
{
    private readonly SourceText _text;
    private readonly SemanticModel _model;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    private int _nextLoopId;

    private ForEachArraySyntaxLowerer(SourceText text, SemanticModel model)
    {
        _text = text;
        _model = model;
    }

    public static ForEachArrayLoweringResult Lower(
        SourceText text,
        CompilationUnitSyntax root,
        SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(model);

        return new ForEachArraySyntaxLowerer(text, model).LowerCompilationUnit(root);
    }

    private ForEachArrayLoweringResult LowerCompilationUnit(CompilationUnitSyntax root)
    {
        var members = ImmutableArray.CreateBuilder<MemberSyntax>(root.Members.Length);
        foreach (var member in root.Members)
        {
            members.Add(member switch
            {
                SubDeclarationSyntax sub => LowerSub(sub),
                FunctionDeclarationSyntax function => LowerFunction(function),
                _ => member
            });
        }

        return new ForEachArrayLoweringResult(
            new CompilationUnitSyntax(members.ToImmutable(), root.EndOfFileToken),
            _diagnostics.ToImmutable());
    }

    private SubDeclarationSyntax LowerSub(SubDeclarationSyntax syntax)
    {
        var procedure = FindProcedure(syntax.Identifier.Text);
        var scope = BuildScope(procedure);
        var usedNames = new HashSet<string>(scope.Keys, StringComparer.OrdinalIgnoreCase);
        return syntax with { Statements = LowerStatements(syntax.Statements, scope, usedNames) };
    }

    private FunctionDeclarationSyntax LowerFunction(FunctionDeclarationSyntax syntax)
    {
        var procedure = FindProcedure(syntax.Identifier.Text);
        var scope = BuildScope(procedure);
        var usedNames = new HashSet<string>(scope.Keys, StringComparer.OrdinalIgnoreCase);
        return syntax with { Statements = LowerStatements(syntax.Statements, scope, usedNames) };
    }

    private BoundProcedure? FindProcedure(string name) =>
        _model.Procedures.FirstOrDefault(procedure =>
            string.Equals(procedure.Symbol.Name, name, StringComparison.OrdinalIgnoreCase));

    private Dictionary<string, VariableSymbol> BuildScope(BoundProcedure? procedure)
    {
        var scope = new Dictionary<string, VariableSymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var moduleVariable in _model.ModuleVariables)
        {
            scope[moduleVariable.Symbol.Name] = moduleVariable.Symbol;
        }

        if (procedure is null)
        {
            return scope;
        }

        foreach (var parameter in procedure.Symbol.Parameters)
        {
            scope[parameter.Name] = parameter;
        }

        foreach (var local in procedure.Locals)
        {
            scope[local.Name] = local;
        }

        if (procedure.Symbol.ReturnType is not null)
        {
            scope[procedure.Symbol.Name] = new ReturnValueSymbol(
                procedure.Symbol.Name,
                procedure.Symbol.ReturnType);
        }

        return scope;
    }

    private ImmutableArray<StatementSyntax> LowerStatements(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, VariableSymbol> scope,
        HashSet<string> usedNames)
    {
        var lowered = ImmutableArray.CreateBuilder<StatementSyntax>();
        foreach (var statement in statements)
        {
            foreach (var replacement in LowerStatement(statement, scope, usedNames))
            {
                lowered.Add(replacement);
            }
        }

        return lowered.ToImmutable();
    }

    private ImmutableArray<StatementSyntax> LowerStatement(
        StatementSyntax statement,
        Dictionary<string, VariableSymbol> scope,
        HashSet<string> usedNames)
    {
        switch (statement)
        {
            case ForEachStatementSyntax forEach:
                return LowerForEach(forEach, scope, usedNames);

            case IfStatementSyntax ifStatement:
            {
                var clauses = ifStatement.ElseIfClauses
                    .Select(clause => clause with
                    {
                        Statements = LowerStatements(clause.Statements, scope, usedNames)
                    })
                    .ToImmutableArray();
                return ImmutableArray.Create<StatementSyntax>(ifStatement with
                {
                    Statements = LowerStatements(ifStatement.Statements, scope, usedNames),
                    ElseIfClauses = clauses,
                    ElseStatements = LowerStatements(ifStatement.ElseStatements, scope, usedNames)
                });
            }

            case ForStatementSyntax forStatement:
                return ImmutableArray.Create<StatementSyntax>(forStatement with
                {
                    Statements = LowerStatements(forStatement.Statements, scope, usedNames)
                });

            case WhileStatementSyntax whileStatement:
                return ImmutableArray.Create<StatementSyntax>(whileStatement with
                {
                    Statements = LowerStatements(whileStatement.Statements, scope, usedNames)
                });

            case DoStatementSyntax doStatement:
                return ImmutableArray.Create<StatementSyntax>(doStatement with
                {
                    Statements = LowerStatements(doStatement.Statements, scope, usedNames)
                });

            case WithStatementSyntax withStatement:
                return ImmutableArray.Create<StatementSyntax>(withStatement with
                {
                    Statements = LowerStatements(withStatement.Statements, scope, usedNames)
                });

            case SelectCaseStatementSyntax selectStatement:
            {
                var cases = selectStatement.Cases
                    .Select(caseBlock => caseBlock with
                    {
                        Statements = LowerStatements(caseBlock.Statements, scope, usedNames)
                    })
                    .ToImmutableArray();
                return ImmutableArray.Create<StatementSyntax>(selectStatement with { Cases = cases });
            }

            default:
                return ImmutableArray.Create(statement);
        }
    }

    private ImmutableArray<StatementSyntax> LowerForEach(
        ForEachStatementSyntax syntax,
        Dictionary<string, VariableSymbol> scope,
        HashSet<string> usedNames)
    {
        if (syntax.Collection is NameExpressionSyntax dynamicCollectionName &&
            scope.TryGetValue(dynamicCollectionName.IdentifierToken.Text, out var dynamicCollectionVariable) &&
            dynamicCollectionVariable.Type is ArrayTypeSymbol { Rank: null })
        {
            return ImmutableArray.Create<StatementSyntax>(syntax with
            {
                Statements = LowerStatements(syntax.Statements, scope, usedNames)
            });
        }

        var valid = true;
        if (!scope.TryGetValue(syntax.Identifier.Text, out var controlVariable))
        {
            Report(
                "VB6S0001",
                $"Variable '{syntax.Identifier.Text}' is not declared.",
                syntax.Identifier.Span);
            valid = false;
        }
        else if (controlVariable.Type != TypeSymbol.Variant && controlVariable.Type != TypeSymbol.Error)
        {
            Report(
                "VB6S0054",
                $"For Each control variable '{syntax.Identifier.Text}' must be Variant when iterating an array.",
                syntax.Identifier.Span);
            valid = false;
        }

        if (syntax.NextIdentifier is not null &&
            !string.Equals(syntax.NextIdentifier.Text, syntax.Identifier.Text, StringComparison.OrdinalIgnoreCase))
        {
            Report(
                "VB6S0013",
                $"Next variable '{syntax.NextIdentifier.Text}' does not match For variable '{syntax.Identifier.Text}'.",
                syntax.NextIdentifier.Span);
            valid = false;
        }

        if (syntax.Collection is not NameExpressionSyntax collectionName)
        {
            Report(
                "VB6S0055",
                "For Each currently requires a fixed array variable as its collection.",
                syntax.InKeyword.Span);
            return ImmutableArray.Create<StatementSyntax>(syntax);
        }

        if (!scope.TryGetValue(collectionName.IdentifierToken.Text, out var collectionVariable))
        {
            Report(
                "VB6S0001",
                $"Variable '{collectionName.IdentifierToken.Text}' is not declared.",
                collectionName.IdentifierToken.Span);
            return ImmutableArray.Create<StatementSyntax>(syntax);
        }

        if (collectionVariable.Type is not ArrayTypeSymbol arrayType || arrayType.Rank is not int rank)
        {
            Report(
                "VB6S0055",
                $"For Each collection '{collectionName.IdentifierToken.Text}' must be a fixed-rank array in the current compiler subset.",
                collectionName.IdentifierToken.Span);
            return ImmutableArray.Create<StatementSyntax>(syntax);
        }

        if (arrayType.ElementType is UserDefinedTypeSymbol)
        {
            Report(
                "VB6S0056",
                "For Each over arrays of user-defined types is not supported.",
                collectionName.IdentifierToken.Span);
            return ImmutableArray.Create<StatementSyntax>(syntax);
        }

        if (!valid)
        {
            return ImmutableArray.Create<StatementSyntax>(syntax);
        }

        var loweredBody = LowerStatements(syntax.Statements, scope, usedNames);
        var loopNumber = _nextLoopId++;
        var indexNames = Enumerable.Range(1, rank)
            .Select(dimension => GetUniqueName($"__vb6_for_each_index_{loopNumber}_{dimension}", usedNames))
            .ToArray();
        var exitFlagName = rank > 1 && ContainsTargetExitFor(loweredBody)
            ? GetUniqueName($"__vb6_for_each_exit_{loopNumber}", usedNames)
            : null;

        if (exitFlagName is not null)
        {
            loweredBody = RewriteTargetExitFor(loweredBody, exitFlagName, nestedForDepth: 0);
        }

        var itemRead = new InvocationExpressionSyntax(
            CloneToken(collectionName.IdentifierToken),
            SyntheticToken(SyntaxKind.OpenParenthesisToken, "(", syntax.ForKeyword.Span.Start),
            indexNames.Select(name => (ExpressionSyntax)new NameExpressionSyntax(
                SyntheticToken(SyntaxKind.IdentifierToken, name, syntax.ForKeyword.Span.Start))).ToImmutableArray(),
            SyntheticToken(SyntaxKind.CloseParenthesisToken, ")", syntax.ForKeyword.Span.Start));
        var assignControl = new AssignmentStatementSyntax(
            CloneToken(syntax.Identifier),
            SyntheticToken(SyntaxKind.EqualsToken, "=", syntax.ForKeyword.Span.Start),
            itemRead);

        var currentBody = ImmutableArray.CreateBuilder<StatementSyntax>();
        currentBody.Add(assignControl);
        currentBody.AddRange(loweredBody);
        var body = currentBody.ToImmutable();

        StatementSyntax? outerLoop = null;
        for (var dimension = rank; dimension >= 1; dimension--)
        {
            var indexName = indexNames[dimension - 1];
            var indexToken = SyntheticToken(SyntaxKind.IdentifierToken, indexName, syntax.ForKeyword.Span.Start);
            var forStatement = new ForStatementSyntax(
                SyntheticToken(SyntaxKind.ForKeyword, "For", syntax.ForKeyword.Span.Start),
                indexToken,
                SyntheticToken(SyntaxKind.EqualsToken, "=", syntax.ForKeyword.Span.Start),
                CreateArrayBoundCall("LBound", collectionName.IdentifierToken, dimension, syntax.ForKeyword.Span.Start),
                SyntheticToken(SyntaxKind.ToKeyword, "To", syntax.ForKeyword.Span.Start),
                CreateArrayBoundCall("UBound", collectionName.IdentifierToken, dimension, syntax.ForKeyword.Span.Start),
                null,
                null,
                body,
                SyntheticToken(SyntaxKind.NextKeyword, "Next", syntax.NextKeyword.Span.Start),
                indexToken);

            outerLoop = forStatement;
            if (dimension > 1)
            {
                var enclosingBody = ImmutableArray.CreateBuilder<StatementSyntax>();
                enclosingBody.Add(forStatement);
                if (exitFlagName is not null)
                {
                    enclosingBody.Add(CreateExitPropagationIf(exitFlagName, syntax.ForKeyword.Span.Start));
                }
                body = enclosingBody.ToImmutable();
            }
        }

        var result = ImmutableArray.CreateBuilder<StatementSyntax>();
        foreach (var indexName in indexNames)
        {
            result.Add(CreateSyntheticLocal(indexName, "Long", SyntaxKind.LongKeyword, syntax.ForKeyword.Span.Start));
        }

        if (exitFlagName is not null)
        {
            result.Add(CreateSyntheticLocal(exitFlagName, "Boolean", SyntaxKind.IdentifierToken, syntax.ForKeyword.Span.Start));
        }

        result.Add(outerLoop!);
        return result.ToImmutable();
    }

    private static InvocationExpressionSyntax CreateArrayBoundCall(
        string name,
        SyntaxToken arrayIdentifier,
        int dimension,
        int position)
    {
        var arguments = ImmutableArray.Create<ExpressionSyntax>(
            new NameExpressionSyntax(CloneToken(arrayIdentifier)),
            new LiteralExpressionSyntax(new SyntaxToken(
                SyntaxKind.IntegerLiteralToken,
                new TextSpan(position, 0),
                dimension.ToString(CultureInfo.InvariantCulture),
                (long)dimension,
                ImmutableArray<SyntaxTrivia>.Empty)));
        return new InvocationExpressionSyntax(
            SyntheticToken(SyntaxKind.IdentifierToken, name, position),
            SyntheticToken(SyntaxKind.OpenParenthesisToken, "(", position),
            arguments,
            SyntheticToken(SyntaxKind.CloseParenthesisToken, ")", position));
    }

    private static DimStatementSyntax CreateSyntheticLocal(
        string name,
        string typeName,
        SyntaxKind typeKind,
        int position) =>
        new(
            SyntheticToken(SyntaxKind.DimKeyword, "Dim", position),
            SyntheticToken(SyntaxKind.IdentifierToken, name, position),
            SyntheticToken(SyntaxKind.AsKeyword, "As", position),
            SyntheticToken(typeKind, typeName, position));

    private static IfStatementSyntax CreateExitPropagationIf(string flagName, int position) =>
        new(
            SyntheticToken(SyntaxKind.IfKeyword, "If", position),
            new NameExpressionSyntax(SyntheticToken(SyntaxKind.IdentifierToken, flagName, position)),
            SyntheticToken(SyntaxKind.ThenKeyword, "Then", position),
            ImmutableArray.Create<StatementSyntax>(new ExitStatementSyntax(
                SyntheticToken(SyntaxKind.ExitKeyword, "Exit", position),
                SyntheticToken(SyntaxKind.ForKeyword, "For", position))),
            ImmutableArray<ElseIfClauseSyntax>.Empty,
            null,
            ImmutableArray<StatementSyntax>.Empty,
            null,
            null,
            true);

    private static bool ContainsTargetExitFor(ImmutableArray<StatementSyntax> statements, int nestedForDepth = 0)
    {
        foreach (var statement in statements)
        {
            if (statement is ExitStatementSyntax exitStatement &&
                exitStatement.TargetKeyword.Kind == SyntaxKind.ForKeyword &&
                nestedForDepth == 0)
            {
                return true;
            }

            if (ContainsTargetExitFor(statement, nestedForDepth))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTargetExitFor(StatementSyntax statement, int nestedForDepth)
    {
        return statement switch
        {
            ForStatementSyntax forStatement => ContainsTargetExitFor(forStatement.Statements, nestedForDepth + 1),
            ForEachStatementSyntax => false,
            IfStatementSyntax ifStatement =>
                ContainsTargetExitFor(ifStatement.Statements, nestedForDepth) ||
                ifStatement.ElseIfClauses.Any(clause => ContainsTargetExitFor(clause.Statements, nestedForDepth)) ||
                ContainsTargetExitFor(ifStatement.ElseStatements, nestedForDepth),
            WhileStatementSyntax whileStatement => ContainsTargetExitFor(whileStatement.Statements, nestedForDepth),
            DoStatementSyntax doStatement => ContainsTargetExitFor(doStatement.Statements, nestedForDepth),
            WithStatementSyntax withStatement => ContainsTargetExitFor(withStatement.Statements, nestedForDepth),
            SelectCaseStatementSyntax selectStatement =>
                selectStatement.Cases.Any(caseBlock => ContainsTargetExitFor(caseBlock.Statements, nestedForDepth)),
            _ => false
        };
    }

    private static ImmutableArray<StatementSyntax> RewriteTargetExitFor(
        ImmutableArray<StatementSyntax> statements,
        string flagName,
        int nestedForDepth)
    {
        var rewritten = ImmutableArray.CreateBuilder<StatementSyntax>();
        foreach (var statement in statements)
        {
            if (statement is ExitStatementSyntax exitStatement &&
                exitStatement.TargetKeyword.Kind == SyntaxKind.ForKeyword &&
                nestedForDepth == 0)
            {
                rewritten.Add(new AssignmentStatementSyntax(
                    SyntheticToken(SyntaxKind.IdentifierToken, flagName, exitStatement.ExitKeyword.Span.Start),
                    SyntheticToken(SyntaxKind.EqualsToken, "=", exitStatement.ExitKeyword.Span.Start),
                    new LiteralExpressionSyntax(SyntheticToken(
                        SyntaxKind.TrueKeyword,
                        "True",
                        exitStatement.ExitKeyword.Span.Start,
                        true))));
                rewritten.Add(exitStatement);
                continue;
            }

            rewritten.Add(RewriteTargetExitFor(statement, flagName, nestedForDepth));
        }

        return rewritten.ToImmutable();
    }

    private static StatementSyntax RewriteTargetExitFor(
        StatementSyntax statement,
        string flagName,
        int nestedForDepth)
    {
        return statement switch
        {
            ForStatementSyntax forStatement => forStatement with
            {
                Statements = RewriteTargetExitFor(forStatement.Statements, flagName, nestedForDepth + 1)
            },
            ForEachStatementSyntax => statement,
            IfStatementSyntax ifStatement => ifStatement with
            {
                Statements = RewriteTargetExitFor(ifStatement.Statements, flagName, nestedForDepth),
                ElseIfClauses = ifStatement.ElseIfClauses.Select(clause => clause with
                {
                    Statements = RewriteTargetExitFor(clause.Statements, flagName, nestedForDepth)
                }).ToImmutableArray(),
                ElseStatements = RewriteTargetExitFor(ifStatement.ElseStatements, flagName, nestedForDepth)
            },
            WhileStatementSyntax whileStatement => whileStatement with
            {
                Statements = RewriteTargetExitFor(whileStatement.Statements, flagName, nestedForDepth)
            },
            DoStatementSyntax doStatement => doStatement with
            {
                Statements = RewriteTargetExitFor(doStatement.Statements, flagName, nestedForDepth)
            },
            WithStatementSyntax withStatement => withStatement with
            {
                Statements = RewriteTargetExitFor(withStatement.Statements, flagName, nestedForDepth)
            },
            SelectCaseStatementSyntax selectStatement => selectStatement with
            {
                Cases = selectStatement.Cases.Select(caseBlock => caseBlock with
                {
                    Statements = RewriteTargetExitFor(caseBlock.Statements, flagName, nestedForDepth)
                }).ToImmutableArray()
            },
            _ => statement
        };
    }

    private static string GetUniqueName(string baseName, HashSet<string> usedNames)
    {
        var name = baseName;
        var suffix = 2;
        while (!usedNames.Add(name))
        {
            name = $"{baseName}_{suffix++}";
        }

        return name;
    }

    private static SyntaxToken CloneToken(SyntaxToken token) =>
        new(token.Kind, token.Span, token.Text, token.Value, token.LeadingTrivia);

    private static SyntaxToken SyntheticToken(
        SyntaxKind kind,
        string text,
        int position,
        object? value = null) =>
        new(kind, new TextSpan(position, 0), text, value, ImmutableArray<SyntaxTrivia>.Empty);

    private void Report(string code, string message, TextSpan span)
    {
        _diagnostics.Add(new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            message,
            span,
            _text.FilePath));
    }
}
