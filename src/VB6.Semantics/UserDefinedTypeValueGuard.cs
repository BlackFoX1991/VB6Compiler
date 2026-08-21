using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Semantics;

/// <summary>
/// Scalar UDT values and fixed arrays of supported primitive values can be lowered as managed
/// value types. This validator keeps dynamic arrays, fixed-length String arrays, arrays of UDTs,
/// and recursive by-value UDT layouts guarded until their VB6 storage semantics are represented
/// explicitly.
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

                case ConstDeclarationSyntax declaration when RequiresStorageGuard(declaration.TypeToken?.Text, types):
                    AddDiagnostic(text, declaration.Identifier.Text, declaration.Identifier.Span, diagnostics);
                    break;

                case SubDeclarationSyntax declaration:
                    ValidateParameters(text, declaration.Parameters, types, diagnostics);
                    ValidateStatements(text, declaration.Statements, types, diagnostics);
                    break;

                case FunctionDeclarationSyntax declaration:
                    // No As clause means Variant, which is never a user-defined type.
                    if (declaration.ReturnTypeToken is not null &&
                        RequiresStorageGuard(declaration.ReturnTypeToken.Text, types))
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
            if (RequiresStorageGuard(parameter.TypeToken.Text, types))
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

                case ForEachStatementSyntax forEachStatement:
                    ValidateStatements(text, forEachStatement.Statements, types, diagnostics);
                    break;

                case WhileStatementSyntax whileStatement:
                    ValidateStatements(text, whileStatement.Statements, types, diagnostics);
                    break;

                case DoStatementSyntax doStatement:
                    ValidateStatements(text, doStatement.Statements, types, diagnostics);
                    break;

                case WithStatementSyntax withStatement:
                    ValidateStatements(text, withStatement.Statements, types, diagnostics);
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
            if (RequiresStorageGuard(declarator.TypeToken?.Text, types))
            {
                AddDiagnostic(text, declarator.Identifier.Text, declarator.TypeToken!.Span, diagnostics);
            }
        }
    }

    private static bool RequiresStorageGuard(
        string? typeName,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> types)
    {
        if (typeName is null || !types.TryGetValue(typeName, out var type))
        {
            return false;
        }

        return RequiresStorageGuard(
            type,
            new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance));
    }

    private static bool RequiresStorageGuard(
        UserDefinedTypeSymbol type,
        HashSet<UserDefinedTypeSymbol> activePath)
    {
        if (!activePath.Add(type))
        {
            return true;
        }

        foreach (var member in type.Members)
        {
            if (member.Type is ArrayTypeSymbol arrayType)
            {
                // A member without bounds is a dynamic array, allocated by ReDim rather than by the
                // enclosing value. The backend already emits it as a plain field and deep-copies it
                // in the clone, so only the element type still has to be one it can lay out.
                if (!IsSupportedArrayElementType(arrayType.ElementType))
                {
                    activePath.Remove(type);
                    return true;
                }

                continue;
            }

            if (member.Type is UserDefinedTypeSymbol nestedType &&
                RequiresStorageGuard(nestedType, activePath))
            {
                activePath.Remove(type);
                return true;
            }
        }

        activePath.Remove(type);
        return false;
    }

    private static bool IsSupportedArrayElementType(TypeSymbol type) =>
        type == TypeSymbol.Byte ||
        type == TypeSymbol.Integer ||
        type == TypeSymbol.Long ||
        type == TypeSymbol.LongLong ||
        type == TypeSymbol.Single ||
        type == TypeSymbol.String ||
        type == TypeSymbol.Boolean ||
        type == TypeSymbol.Double ||
        type == TypeSymbol.Currency;

    private static void AddDiagnostic(
        SourceText text,
        string valueName,
        TextSpan span,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        diagnostics.Add(new Diagnostic(
            "VB6S0046",
            DiagnosticSeverity.Error,
            $"User-defined type value '{valueName}' uses a UDT layout that is not supported by managed lowering yet.",
            span,
            text.FilePath));
    }
}
