using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax.Nodes;

namespace VB6.Compiler;

/// <summary>
/// VB6 language intrinsics made visible to the binder as ordinary procedures.
///
/// Each symbol carries the runtime method the backend calls, so an intrinsic travels the normal
/// call path: the binder resolves it, checks the argument count, and converts the arguments like
/// any other procedure, while only the backend knows the C# name. User declarations of the same
/// name keep precedence, which is what VB6 does.
/// </summary>
internal static class VBIntrinsicSymbols
{
    private static readonly ImmutableArray<ProcedureSymbol> Intrinsics = ImmutableArray.Create(
        // Strings
        Function("Len", "VBStrings.Len", TypeSymbol.Long, Parameter("Expression", TypeSymbol.Variant)),
        // Mid(s, start) runs to the end of the string; the runtime carries an overload for each arity.
        Function(
            "Mid",
            "VBStrings.Mid",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Start", TypeSymbol.Long),
            Parameter("Length", TypeSymbol.Long)) with { IntrinsicMinimumArguments = 2 },
        Function("Chr", "VBStrings.Chr", TypeSymbol.String, Parameter("CharCode", TypeSymbol.Long)),
        Function(
            "Left",
            "VBStrings.Left",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function(
            "Right",
            "VBStrings.Right",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function("UCase", "VBStrings.UCase", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("LCase", "VBStrings.LCase", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("Trim", "VBStrings.Trim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("LTrim", "VBStrings.LTrim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("RTrim", "VBStrings.RTrim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("Asc", "VBStrings.Asc", TypeSymbol.Long, Parameter("Expression", TypeSymbol.String)),
        Function("IsNumeric", "VBStrings.IsNumeric", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),

        // Conversions. VB6 spells the checked conversions the runtime already implements.
        Function("CByte", "VBConversions.CByte", TypeSymbol.Byte, Parameter("Expression", TypeSymbol.Variant)),
        Function("CInt", "VBConversions.CInt", TypeSymbol.Integer, Parameter("Expression", TypeSymbol.Variant)),
        Function("CLng", "VBConversions.CLng", TypeSymbol.Long, Parameter("Expression", TypeSymbol.Variant)),
        Function("CSng", "VBConversions.CSng", TypeSymbol.Single, Parameter("Expression", TypeSymbol.Variant)),
        Function("CDbl", "VBConversions.CDbl", TypeSymbol.Double, Parameter("Expression", TypeSymbol.Variant)),
        Function("CBool", "VBConversions.CBool", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("CStr", "VBConversions.CStr", TypeSymbol.String, Parameter("Expression", TypeSymbol.Variant)));

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

        foreach (var intrinsic in Intrinsics)
        {
            // A user procedure of the same name wins, as it does in VB6.
            if (!procedures.ContainsKey(intrinsic.Name))
            {
                procedures.Add(intrinsic.Name, intrinsic);
            }
        }
    }

    private static ProcedureSymbol Function(
        string name,
        string runtimeTarget,
        TypeSymbol returnType,
        params ParameterSymbol[] parameters) =>
        new(name, parameters.ToImmutableArray(), returnType) { IntrinsicTarget = runtimeTarget };

    private static ParameterSymbol Parameter(string name, TypeSymbol type) =>
        new(name, type, ParameterPassingMode.ByVal);
}
