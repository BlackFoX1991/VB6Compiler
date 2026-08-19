using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Semantics;

/// <summary>
/// Declares and resolves module-level VB6 user-defined types independently from procedure/body
/// binding. The first pass creates stable type identities; the second pass resolves member types
/// against those identities, which permits forward references between Type declarations.
/// </summary>
public sealed class UserDefinedTypeDeclarationBinder
{
    private readonly SourceText _text;
    private readonly IReadOnlyDictionary<string, UserDefinedTypeSymbol> _externalTypes;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics =
        ImmutableArray.CreateBuilder<Diagnostic>();

    public UserDefinedTypeDeclarationBinder(
        SourceText text,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol>? externalTypes = null)
    {
        _text = text;
        _externalTypes = externalTypes ??
            new Dictionary<string, UserDefinedTypeSymbol>(StringComparer.OrdinalIgnoreCase);
    }

    public UserDefinedTypeDeclarationResult Bind(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var declarations = root.Members.OfType<TypeDeclarationSyntax>().ToImmutableArray();
        var types = new Dictionary<string, UserDefinedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var declaration in declarations)
        {
            var symbol = new UserDefinedTypeSymbol(declaration.Identifier.Text);
            if (!types.TryAdd(symbol.Name, symbol))
            {
                Report(
                    "VB6S0036",
                    $"User-defined type '{symbol.Name}' is already declared in this module.",
                    declaration.Identifier.Span);
            }
        }

        foreach (var declaration in declarations)
        {
            if (!types.TryGetValue(declaration.Identifier.Text, out var type) || type.MembersDefined)
            {
                continue;
            }

            var members = ImmutableArray.CreateBuilder<UserDefinedTypeMemberSymbol>();
            foreach (var memberSyntax in declaration.Members)
            {
                var memberType = ResolveMemberType(memberSyntax, types);
                members.Add(new UserDefinedTypeMemberSymbol(memberSyntax.Identifier.Text, memberType));
            }

            if (!type.TryDefineMembers(members, out var duplicateMemberName))
            {
                var duplicateSyntax = declaration.Members.First(member =>
                    string.Equals(
                        member.Identifier.Text,
                        duplicateMemberName,
                        StringComparison.OrdinalIgnoreCase));
                Report(
                    "VB6S0037",
                    $"Member '{duplicateMemberName}' is already declared in user-defined type '{type.Name}'.",
                    duplicateSyntax.Identifier.Span);
            }
        }

        return new UserDefinedTypeDeclarationResult(
            types.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
            _diagnostics.ToImmutable());
    }

    private TypeSymbol ResolveMemberType(
        TypeMemberSyntax member,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> localTypes)
    {
        var elementType = ResolveType(member.TypeToken, localTypes);
        if (elementType == TypeSymbol.Error)
        {
            return TypeSymbol.Error;
        }

        if (member.IsFixedLengthString)
        {
            if (elementType != TypeSymbol.String)
            {
                Report(
                    "VB6S0038",
                    $"Fixed-length declaration for member '{member.Identifier.Text}' requires String.",
                    member.TypeToken.Span);
                return TypeSymbol.Error;
            }

            var length = BindFixedStringLength(member);
            if (length is null)
            {
                return TypeSymbol.Error;
            }

            elementType = new FixedLengthStringTypeSymbol(length.Value);
        }

        if (!member.IsArray)
        {
            return elementType;
        }

        return member.Dimensions.IsDefaultOrEmpty
            ? new ArrayTypeSymbol(elementType)
            : new ArrayTypeSymbol(elementType, member.Dimensions.Length);
    }

    private TypeSymbol ResolveType(
        SyntaxToken typeToken,
        IReadOnlyDictionary<string, UserDefinedTypeSymbol> localTypes)
    {
        var primitive = TypeSymbol.Lookup(typeToken.Text);
        if (primitive is not null)
        {
            return primitive;
        }

        if (localTypes.TryGetValue(typeToken.Text, out var localType))
        {
            return localType;
        }

        if (_externalTypes.TryGetValue(typeToken.Text, out var externalType))
        {
            return externalType;
        }

        Report("VB6S0003", $"Unknown type '{typeToken.Text}'.", typeToken.Span);
        return TypeSymbol.Error;
    }

    private int? BindFixedStringLength(TypeMemberSyntax member)
    {
        if (member.FixedStringLength is not LiteralExpressionSyntax literal ||
            literal.LiteralToken.Kind != SyntaxKind.IntegerLiteralToken)
        {
            Report(
                "VB6S0039",
                $"Fixed-length String member '{member.Identifier.Text}' requires an integer constant length in the current compiler subset.",
                member.StarToken?.Span ?? member.Identifier.Span);
            return null;
        }

        var value = Convert.ToInt64(literal.LiteralToken.Value, System.Globalization.CultureInfo.InvariantCulture);
        if (value is < 1 or > 65526)
        {
            Report(
                "VB6S0040",
                $"Fixed-length String member '{member.Identifier.Text}' must contain between 1 and 65526 characters.",
                literal.LiteralToken.Span);
            return null;
        }

        return checked((int)value);
    }

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

public sealed record UserDefinedTypeDeclarationResult(
    ImmutableDictionary<string, UserDefinedTypeSymbol> Types,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}
