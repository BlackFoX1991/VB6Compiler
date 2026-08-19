using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Semantics;

/// <summary>
/// Builds the VB6 UDT type space for a set of modules. Public Type declarations share one stable
/// project identity; Private Type declarations are created separately by each module binder.
/// </summary>
public sealed class ProjectUserDefinedTypeDeclarationBinder
{
    public ProjectUserDefinedTypeDeclarationResult Bind(
        IEnumerable<UserDefinedTypeModuleInput> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var moduleArray = modules.ToImmutableArray();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var publicTypes = new Dictionary<string, UserDefinedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in moduleArray)
        {
            foreach (var declaration in module.Root.Members.OfType<TypeDeclarationSyntax>())
            {
                if (UserDefinedTypeDeclarationBinder.IsPrivate(declaration))
                {
                    continue;
                }

                var type = new UserDefinedTypeSymbol(declaration.Identifier.Text);
                if (!publicTypes.TryAdd(type.Name, type))
                {
                    diagnostics.Add(new Diagnostic(
                        "VB6S0041",
                        DiagnosticSeverity.Error,
                        $"Public user-defined type '{type.Name}' is already declared in this project.",
                        declaration.Identifier.Span,
                        module.Text.FilePath));
                }
            }
        }

        var moduleResults = ImmutableArray.CreateBuilder<UserDefinedTypeModuleResult>(moduleArray.Length);
        foreach (var module in moduleArray)
        {
            var result = new UserDefinedTypeDeclarationBinder(module.Text, publicTypes).Bind(module.Root);
            diagnostics.AddRange(result.Diagnostics);
            moduleResults.Add(new UserDefinedTypeModuleResult(module, result.Types));
        }

        return new ProjectUserDefinedTypeDeclarationResult(
            publicTypes.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
            moduleResults.ToImmutable(),
            diagnostics.ToImmutable());
    }
}

public sealed record UserDefinedTypeModuleInput(
    SourceText Text,
    CompilationUnitSyntax Root);

public sealed record UserDefinedTypeModuleResult(
    UserDefinedTypeModuleInput Module,
    ImmutableDictionary<string, UserDefinedTypeSymbol> Types);

public sealed record ProjectUserDefinedTypeDeclarationResult(
    ImmutableDictionary<string, UserDefinedTypeSymbol> PublicTypes,
    ImmutableArray<UserDefinedTypeModuleResult> Modules,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}
