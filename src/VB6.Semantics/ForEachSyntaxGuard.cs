using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Semantics;

/// <summary>
/// Keeps parsed For Each loops from being silently dropped until their binding and lowering
/// semantics are implemented. Nested statements are scanned recursively so every reachable
/// For Each loop is diagnosed explicitly.
/// </summary>
public static class ForEachSyntaxGuard
{
    public static ImmutableArray<Diagnostic> Validate(SourceText text, CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var member in root.Members)
        {
            switch (member)
            {
                case SubDeclarationSyntax sub:
                    ValidateStatements(text, sub.Statements, diagnostics);
                    break;
                case FunctionDeclarationSyntax function:
                    ValidateStatements(text, function.Statements, diagnostics);
                    break;
            }
        }

        return diagnostics.ToImmutable();
    }

    private static void ValidateStatements(
        SourceText text,
        ImmutableArray<StatementSyntax> statements,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ForEachStatementSyntax forEach:
                    diagnostics.Add(new Diagnostic(
                        "VB6S0052",
                        DiagnosticSeverity.Error,
                        "For Each semantics are not implemented yet.",
                        forEach.ForKeyword.Span,
                        text.FilePath));
                    ValidateStatements(text, forEach.Statements, diagnostics);
                    break;

                case IfStatementSyntax ifStatement:
                    ValidateStatements(text, ifStatement.Statements, diagnostics);
                    foreach (var elseIf in ifStatement.ElseIfClauses)
                    {
                        ValidateStatements(text, elseIf.Statements, diagnostics);
                    }
                    ValidateStatements(text, ifStatement.ElseStatements, diagnostics);
                    break;

                case ForStatementSyntax forStatement:
                    ValidateStatements(text, forStatement.Statements, diagnostics);
                    break;

                case WhileStatementSyntax whileStatement:
                    ValidateStatements(text, whileStatement.Statements, diagnostics);
                    break;

                case DoStatementSyntax doStatement:
                    ValidateStatements(text, doStatement.Statements, diagnostics);
                    break;

                case WithStatementSyntax withStatement:
                    ValidateStatements(text, withStatement.Statements, diagnostics);
                    break;

                case SelectCaseStatementSyntax selectStatement:
                    foreach (var caseBlock in selectStatement.Cases)
                    {
                        ValidateStatements(text, caseBlock.Statements, diagnostics);
                    }
                    break;
            }
        }
    }
}
