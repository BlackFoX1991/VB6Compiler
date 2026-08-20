using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Compiler;

internal static class FileIoSyntaxGuard
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
                    VisitStatements(text, sub.Statements, diagnostics);
                    break;
                case FunctionDeclarationSyntax function:
                    VisitStatements(text, function.Statements, diagnostics);
                    break;
            }
        }

        return diagnostics.ToImmutable();
    }

    private static void VisitStatements(
        SourceText text,
        ImmutableArray<StatementSyntax> statements,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case FileIoStatementSyntax fileIo:
                    diagnostics.Add(new Diagnostic(
                        "VB6S0057",
                        DiagnosticSeverity.Error,
                        $"VB6 file I/O statement '{fileIo.KeywordToken.Text}' is parsed but not implemented yet.",
                        fileIo.KeywordToken.Span,
                        text.FilePath));
                    break;

                case IfStatementSyntax ifStatement:
                    VisitStatements(text, ifStatement.Statements, diagnostics);
                    foreach (var clause in ifStatement.ElseIfClauses)
                    {
                        VisitStatements(text, clause.Statements, diagnostics);
                    }
                    VisitStatements(text, ifStatement.ElseStatements, diagnostics);
                    break;

                case ForStatementSyntax forStatement:
                    VisitStatements(text, forStatement.Statements, diagnostics);
                    break;

                case ForEachStatementSyntax forEachStatement:
                    VisitStatements(text, forEachStatement.Statements, diagnostics);
                    break;

                case WhileStatementSyntax whileStatement:
                    VisitStatements(text, whileStatement.Statements, diagnostics);
                    break;

                case DoStatementSyntax doStatement:
                    VisitStatements(text, doStatement.Statements, diagnostics);
                    break;

                case WithStatementSyntax withStatement:
                    VisitStatements(text, withStatement.Statements, diagnostics);
                    break;

                case SelectCaseStatementSyntax selectStatement:
                    foreach (var caseBlock in selectStatement.Cases)
                    {
                        VisitStatements(text, caseBlock.Statements, diagnostics);
                    }
                    break;
            }
        }
    }
}
