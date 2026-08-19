using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax.Nodes;

namespace VB6.Compiler;

internal static class VBIntrinsicSymbols
{
    private const string LenLookupName = "Len";
    private const string LenGeneratedName = "__VB6_INTRINSIC_LEN";
    private const string LenGeneratedCall = "__vb6___VB6_INTRINSIC_LEN(";

    private static readonly ProcedureSymbol LenSymbol = new(
        LenGeneratedName,
        ImmutableArray.Create(new ParameterSymbol(
            "Expression",
            TypeSymbol.Variant,
            ParameterPassingMode.ByVal)),
        TypeSymbol.Long);

    public static Dictionary<string, ProcedureSymbol> CreateProcedureTable(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in root.Members)
        {
            var symbol = member switch
            {
                SubDeclarationSyntax sub => Binder.CreateProcedureSymbol(sub),
                FunctionDeclarationSyntax function => Binder.CreateProcedureSymbol(function),
                _ => null
            };

            if (symbol is not null)
            {
                procedures.TryAdd(symbol.Name, symbol);
            }
        }

        AddTo(procedures);
        return procedures;
    }

    public static void AddTo(IDictionary<string, ProcedureSymbol> procedures)
    {
        ArgumentNullException.ThrowIfNull(procedures);
        if (!procedures.ContainsKey(LenLookupName))
        {
            procedures.Add(LenLookupName, LenSymbol);
        }
    }

    public static string RewriteGeneratedCalls(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Replace(
            LenGeneratedCall,
            "VBStrings.Len(",
            StringComparison.Ordinal);
    }
}
