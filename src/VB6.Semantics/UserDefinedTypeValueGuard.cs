using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Semantics;

/// <summary>
/// UDT declarations and value types can already bind, but the managed storage/layout lowering is
/// not implemented yet. This validator keeps those bound types visible to later compiler layers
/// while preventing the current C# generator from silently lowering them as object?.
/// </summary>
public static class UserDefinedTypeValueGuard
{
    public static ImmutableArray<Diagnostic> Validate(
        SourceText text,
        CompilationUnitSyntax root,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> types)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(types);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        foreach (var member in root.Members)
        {
            switch (member)
            {
                case ModuleVariableDeclarationSyntax declaration:
                    ValidateDeclarators(text, declaration.Declarators, types, diagnostics);
                    break;

                case ConstDeclarationSyntax declaration when IsUserDefinedType(declaration.TypeToken?.Text, types):
                    AddDiagnostic(text, declaration.Identifier.Text, declaration.Identifier.Span, diagnostics);
                    break;

                case SubDeclarationSyntax declaration:
                    ValidateParameters(text, declaration.Parameters, types, diagnostics);
                    ValidateStatements(text, declaration.Statements, types, diagnostics);
                    break;

                case FunctionDeclarationSyntax declaration:
                    if (IsUserDefinedType(declaration.ReturnTypeToken.Text, types))
                    {
                        AddDiagnostic(text, declaration.Identifier.Text, declaration.ReturnTypeToken.Span, diagnostics);
                    }

                    ValidateParameters(text, declaration.Parameters, types, diagnostics);
                    ValidateStatements(text, declaration.Statements, types, diagnostics);
                    break;
            }
        }

        return diagnostics.ToImmutable();
    }

    private static void ValidateParameters(
        SourceText text,
        ImmutableArray<ParameterSyntax> parameters,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> types,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var parameter in parameters)
        {
            if (IsUserDefinedType(parameter.TypeToken.Text, types))
            {
                AddDiagnostic(text, parameter.Identifier.Text, parameter.TypeToken.Span, diagnostics);
            }
        }
    }

    private static void ValidateStatements(
        SourceText text,
        ImmutableArray<StatementSyntax> statements,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> types,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DimStatementSyntax dim:
                    ValidateDeclarators(text, dim.Declarators, types, diagnostics);
                    break;

                case ReDimStatementSyntax reDim:
                    ValidateDeclarators(text, reDim.Declarators, types, diagnostics);
                    break;

                case StaticStatementSyntax staticStatement:
                    ValidateDeclarators(text, staticStatement.Declarators, types, diagnostics);
                    break;

                case IfStatementSyntax ifStatement:
                    ValidateStatements(text, ifStatement.Statements, types, diagnostics);
                    foreach (var elseIf in ifStatement.ElseIfClauses)
                    {
                        ValidateStatements(text, elseIf.Statements, types, diagnostics);
                    }
                    ValidateStatements(text, ifStatement.ElseStatements, types, diagnostics);
                    break;

                case ForStatementSyntax forStatement:
                    ValidateStatements(text, forStatement.Statements, types, diagnostics);
                    break;

                case WhileStatementSyntax whileStatement:
                    ValidateStatements(text, whileStatement.Statements, types, diagnostics);
                    break;

                case DoStatementSyntax doStatement:
                    ValidateStatements(text, doStatement.Statements, types, diagnostics);
                    break;

                case SelectCaseStatementSyntax selectStatement:
                    foreach (var caseBlock in selectStatement.Cases)
                    {
                        ValidateStatements(text, caseBlock.Statements, types, diagnostics);
                    }
                    break;
            }
        }
    }

    private static void ValidateDeclarators(
        SourceText text,
        ImmutableArray<VariableDeclaratorSyntax> declarators,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> types,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var declarator in declarators)
        {
            if (IsUserDefinedType(declarator.TypeToken?.Text, types))
            {
                AddDiagnostic(text, declarator.Identifier.Text, declarator.TypeToken!.Span, diagnostics);
            }
        }
    }

    private static bool IsUserDefinedType(
        string? typeName,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> types) =>
        typeName is not null && types.ContainsKey(typeName);

    private static void AddDiagnostic(
        SourceText text,
        string valueName,
        TextSpan span,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        diagnostics.Add(new Diagnostic(
            "VB6S0046",
            DiagnosticSeverity.Error,
            $"User-defined type value '{valueName}' is bound, but managed UDT storage/code generation is not implemented yet.",
            span,
            text.FilePath));
    }
}
