using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VB6.Runtime;

namespace VB6.CodeGen.CSharp;

public sealed class CSharpAssemblyEmitter
{
    public AssemblyEmitResult Emit(string source, string assemblyName, Stream peStream)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentNullException.ThrowIfNull(peStream);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication)
            .WithOptimizationLevel(OptimizationLevel.Release)
            .WithDeterministic(true);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            GetMetadataReferences(),
            options);

        var result = compilation.Emit(peStream);
        var diagnostics = result.Diagnostics
            .Where(diagnostic => diagnostic.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
            .Select(diagnostic => new AssemblyEmitDiagnostic(
                diagnostic.Id,
                diagnostic.Severity,
                diagnostic.GetMessage()))
            .ToImmutableArray();

        return new AssemblyEmitResult(result.Success, diagnostics);
    }

    private static ImmutableArray<MetadataReference> GetMetadataReferences()
    {
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }
        else
        {
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        }

        references.Add(MetadataReference.CreateFromFile(typeof(VBConversions).Assembly.Location));
        return references.ToImmutable();
    }
}

public sealed record AssemblyEmitDiagnostic(
    string Id,
    Microsoft.CodeAnalysis.DiagnosticSeverity Severity,
    string Message);

public sealed record AssemblyEmitResult(
    bool Success,
    ImmutableArray<AssemblyEmitDiagnostic> Diagnostics);
