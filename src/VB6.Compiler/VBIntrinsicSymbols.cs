using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax.Nodes;

namespace VB6.Compiler;

internal static class VBIntrinsicSymbols
{
    private const string LenLookupName = "Len";
    private const string LenGeneratedName = "__VB6_INTRINSIC_LEN";
    private const string LenGeneratedCall = "__vb6___VB6_INTRINSIC_LEN(";

    private const string MidLookupName = "Mid";
    private const string MidGeneratedName = "__VB6_INTRINSIC_MID";
    private const string MidGeneratedCall = "__vb6___VB6_INTRINSIC_MID(";

    private const string ChrLookupName = "Chr";
    private const string ChrGeneratedName = "__VB6_INTRINSIC_CHR";
    private const string ChrGeneratedCall = "__vb6___VB6_INTRINSIC_CHR(";

    private static readonly ProcedureSymbol LenSymbol = new(
        LenGeneratedName,
        ImmutableArray.Create(new ParameterSymbol(
            "Expression",
            TypeSymbol.Variant,
            ParameterPassingMode.ByVal)),
        TypeSymbol.Long);

    private static readonly ProcedureSymbol MidSymbol = new(
        MidGeneratedName,
        ImmutableArray.Create(
            new ParameterSymbol("Expression", TypeSymbol.String, ParameterPassingMode.ByVal),
            new ParameterSymbol("Start", TypeSymbol.Long, ParameterPassingMode.ByVal),
            new ParameterSymbol("Length", TypeSymbol.Long, ParameterPassingMode.ByVal)),
        TypeSymbol.String);

    private static readonly ProcedureSymbol ChrSymbol = new(
        ChrGeneratedName,
        ImmutableArray.Create(new ParameterSymbol(
            "CharCode",
            TypeSymbol.Long,
            ParameterPassingMode.ByVal)),
        TypeSymbol.String);

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
        AddIfMissing(procedures, LenLookupName, LenSymbol);
        AddIfMissing(procedures, MidLookupName, MidSymbol);
        AddIfMissing(procedures, ChrLookupName, ChrSymbol);
    }

    public static string RewriteGeneratedCalls(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source
            .Replace(LenGeneratedCall, "VBStrings.Len(", StringComparison.Ordinal)
            .Replace(MidGeneratedCall, "VBStrings.Mid(", StringComparison.Ordinal)
            .Replace(ChrGeneratedCall, "VBStrings.Chr(", StringComparison.Ordinal);
    }

    private static void AddIfMissing(
        IDictionary<string, ProcedureSymbol> procedures,
        string name,
        ProcedureSymbol symbol)
    {
        if (!procedures.ContainsKey(name))
        {
            procedures.Add(name, symbol);
        }
    }
}
