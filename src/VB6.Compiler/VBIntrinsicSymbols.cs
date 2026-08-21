using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax.Nodes;

namespace VB6.Compiler;

/// <summary>
/// VB6 language intrinsics made visible to the binder as ordinary procedures. Symbols carry a
/// backend-independent intrinsic identity. The legacy runtime-target string remains populated only
/// while the C# backend is still present during the parity cutover.
/// </summary>
internal static class VBIntrinsicSymbols
{
    private static readonly ImmutableArray<ProcedureSymbol> Intrinsics = ImmutableArray.Create(
        Function("Len", VBIntrinsicKind.Len, "VBStrings.Len", TypeSymbol.Long, Parameter("Expression", TypeSymbol.Variant)),
        Function(
            "Mid",
            VBIntrinsicKind.Mid,
            "VBStrings.Mid",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Start", TypeSymbol.Long),
            Parameter("Length", TypeSymbol.Long)) with { IntrinsicMinimumArguments = 2 },
        Function("Chr", VBIntrinsicKind.Chr, "VBStrings.Chr", TypeSymbol.String, Parameter("CharCode", TypeSymbol.Long)),
        Function(
            "Left",
            VBIntrinsicKind.Left,
            "VBStrings.Left",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function(
            "Right",
            VBIntrinsicKind.Right,
            "VBStrings.Right",
            TypeSymbol.String,
            Parameter("Expression", TypeSymbol.String),
            Parameter("Length", TypeSymbol.Long)),
        Function("UCase", VBIntrinsicKind.UCase, "VBStrings.UCase", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("LCase", VBIntrinsicKind.LCase, "VBStrings.LCase", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("Trim", VBIntrinsicKind.Trim, "VBStrings.Trim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("LTrim", VBIntrinsicKind.LTrim, "VBStrings.LTrim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("RTrim", VBIntrinsicKind.RTrim, "VBStrings.RTrim", TypeSymbol.String, Parameter("Expression", TypeSymbol.String)),
        Function("Asc", VBIntrinsicKind.Asc, "VBStrings.Asc", TypeSymbol.Long, Parameter("Expression", TypeSymbol.String)),
        Function("IsNumeric", VBIntrinsicKind.IsNumeric, "VBStrings.IsNumeric", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),

        Function("FreeFile", VBIntrinsicKind.FreeFile, "VBFiles.FreeFile", TypeSymbol.Long),
        Function("LOF", VBIntrinsicKind.LOF, "VBFiles.Length", TypeSymbol.LongLong, Parameter("FileNumber", TypeSymbol.Long)),
        Function("EOF", VBIntrinsicKind.EOF, "VBFiles.EndOfFile", TypeSymbol.Boolean, Parameter("FileNumber", TypeSymbol.Long)),
        Function("Seek", VBIntrinsicKind.Seek, "VBFiles.Position", TypeSymbol.LongLong, Parameter("FileNumber", TypeSymbol.Long)),

        Function("CByte", VBIntrinsicKind.CByte, "VBConversions.CByte", TypeSymbol.Byte, Parameter("Expression", TypeSymbol.Variant)),
        Function("CInt", VBIntrinsicKind.CInt, "VBConversions.CInt", TypeSymbol.Integer, Parameter("Expression", TypeSymbol.Variant)),
        Function("CLng", VBIntrinsicKind.CLng, "VBConversions.CLng", TypeSymbol.Long, Parameter("Expression", TypeSymbol.Variant)),
        Function("CSng", VBIntrinsicKind.CSng, "VBConversions.CSng", TypeSymbol.Single, Parameter("Expression", TypeSymbol.Variant)),
        Function("CDbl", VBIntrinsicKind.CDbl, "VBConversions.CDbl", TypeSymbol.Double, Parameter("Expression", TypeSymbol.Variant)),
        Function("CBool", VBIntrinsicKind.CBool, "VBConversions.CBool", TypeSymbol.Boolean, Parameter("Expression", TypeSymbol.Variant)),
        Function("CStr", VBIntrinsicKind.CStr, "VBConversions.CStr", TypeSymbol.String, Parameter("Expression", TypeSymbol.Variant)));

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
            if (!procedures.ContainsKey(intrinsic.Name))
            {
                procedures.Add(intrinsic.Name, intrinsic);
            }
        }
    }

    private static ProcedureSymbol Function(
        string name,
        VBIntrinsicKind intrinsicKind,
        string runtimeTarget,
        TypeSymbol returnType,
        params ParameterSymbol[] parameters) =>
        new(name, parameters.ToImmutableArray(), returnType)
        {
            IntrinsicKind = intrinsicKind,
            IntrinsicTarget = runtimeTarget
        };

    private static ParameterSymbol Parameter(string name, TypeSymbol type) =>
        new(name, type, ParameterPassingMode.ByVal);
}
