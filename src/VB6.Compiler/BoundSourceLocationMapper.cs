using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using VB6.Semantics;
using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Compiler;

/// <summary>
/// Attaches source locations after all syntax-side normalization has finished. The mapper walks the
/// final syntax tree and bound tree in statement order; one source statement may deliberately map
/// to several bound statements (for example a comma-separated Dim/ReDim/Erase).
/// </summary>
internal static class BoundSourceLocationMapper
{
    public static SemanticModel Attach(
        SourceText text,
        CompilationUnitSyntax semanticRoot,
        SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(semanticRoot);
        ArgumentNullException.ThrowIfNull(model);

        var syntaxProcedures = semanticRoot.Members
            .Select(member => member switch
            {
                SubDeclarationSyntax sub => new SyntaxProcedure(sub.Identifier.Text, sub.Statements),
                FunctionDeclarationSyntax function => new SyntaxProcedure(function.Identifier.Text, function.Statements),
                _ => null
            })
            .Where(procedure => procedure is not null)
            .Cast<SyntaxProcedure>()
            .ToDictionary(procedure => procedure.Name, StringComparer.OrdinalIgnoreCase);

        var procedures = model.Procedures.Select(procedure =>
        {
            if (!syntaxProcedures.TryGetValue(procedure.Symbol.Name, out var syntax))
            {
                return procedure;
            }

            using var locations = EnumerateLocations(text, syntax.Statements).GetEnumerator();
            return procedure with { Body = MapBlock(procedure.Body, locations) };
        }).ToImmutableArray();

        return model with { Procedures = procedures };
    }

    private static BoundBlockStatement MapBlock(
        BoundBlockStatement block,
        IEnumerator<SourceLocation> locations)
    {
        var statements = block.Statements
            .Select(statement => MapStatement(statement, locations))
            .ToImmutableArray();
        return block with { Statements = statements };
    }

    private static BoundStatement MapStatement(
        BoundStatement statement,
        IEnumerator<SourceLocation> locations)
    {
        var location = locations.MoveNext() ? locations.Current : statement.SourceLocation;
        BoundStatement mapped = statement switch
        {
            BoundIfStatement @if => @if with
            {
                Body = MapBlock(@if.Body, locations),
                ElseIfClauses = @if.ElseIfClauses.Select(clause => clause with
                {
                    Body = MapBlock(clause.Body, locations)
                }).ToImmutableArray(),
                ElseBody = @if.ElseBody is null ? null : MapBlock(@if.ElseBody, locations)
            },
            BoundForStatement @for => @for with { Body = MapBlock(@for.Body, locations) },
            BoundForEachStatement forEach => forEach with { Body = MapBlock(forEach.Body, locations) },
            BoundWhileStatement @while => @while with { Body = MapBlock(@while.Body, locations) },
            BoundDoStatement @do => @do with { Body = MapBlock(@do.Body, locations) },
            BoundWithStatement with => with with { Body = MapBlock(with.Body, locations) },
            BoundSelectCaseStatement select => select with
            {
                Cases = select.Cases.Select(@case => @case with
                {
                    Body = MapBlock(@case.Body, locations)
                }).ToImmutableArray()
            },
            _ => statement
        };

        return location is null ? mapped : mapped with { SourceLocation = location };
    }

    private static IEnumerable<SourceLocation> EnumerateLocations(
        SourceText text,
        ImmutableArray<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            var span = GetStatementSpan(statement);
            var location = new SourceLocation(text.FilePath, span);
            for (var repeat = 0; repeat < GetBoundStatementMultiplicity(statement); repeat++)
            {
                yield return location;
            }

            foreach (var child in GetDirectChildStatements(statement)
                         .OrderBy(child => GetStatementSpan(child).Start))
            {
                foreach (var childLocation in EnumerateLocations(text, ImmutableArray.Create(child)))
                {
                    yield return childLocation;
                }
            }
        }
    }

    private static int GetBoundStatementMultiplicity(StatementSyntax statement) => statement switch
    {
        DimStatementSyntax dim => Math.Max(1, dim.Declarators.Length),
        ReDimStatementSyntax reDim => Math.Max(1, reDim.Declarators.Length + reDim.QualifiedTargets.Length),
        EraseStatementSyntax erase => Math.Max(1, erase.Identifiers.Length),
        _ => 1
    };

    private static IEnumerable<StatementSyntax> GetDirectChildStatements(StatementSyntax statement)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var property in statement.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var child in FindStatements(property.GetValue(statement), seen))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<StatementSyntax> FindStatements(object? value, HashSet<object> seen)
    {
        if (value is null || value is string || value is SyntaxToken)
        {
            yield break;
        }

        if (value is StatementSyntax statement)
        {
            yield return statement;
            yield break;
        }

        if (value is SyntaxNode node)
        {
            if (!seen.Add(node))
            {
                yield break;
            }

            foreach (var property in node.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                foreach (var statement in FindStatements(property.GetValue(node), seen))
                {
                    yield return statement;
                }
            }
            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                foreach (var statement in FindStatements(item, seen))
                {
                    yield return statement;
                }
            }
        }
    }

    private static TextSpan GetStatementSpan(StatementSyntax statement)
    {
        var tokens = new List<SyntaxToken>();
        CollectTokens(statement, tokens, new HashSet<object>(ReferenceEqualityComparer.Instance));
        if (tokens.Count == 0)
        {
            return new TextSpan(0, 0);
        }

        var first = tokens.MinBy(token => token.Span.Start)!;
        return first.Span;
    }

    private static void CollectTokens(object? value, List<SyntaxToken> tokens, HashSet<object> seen)
    {
        if (value is null || value is string)
        {
            return;
        }

        if (value is SyntaxToken token)
        {
            tokens.Add(token);
            return;
        }

        if (value is SyntaxNode node)
        {
            if (!seen.Add(node))
            {
                return;
            }

            foreach (var property in node.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                CollectTokens(property.GetValue(node), tokens, seen);
            }
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                CollectTokens(item, tokens, seen);
            }
        }
    }

    private sealed record SyntaxProcedure(string Name, ImmutableArray<StatementSyntax> Statements);
}
