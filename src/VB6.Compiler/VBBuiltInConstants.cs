using System.Collections.Immutable;
using VB6.Semantics;

namespace VB6.Compiler;

/// <summary>
/// Project-global VB/VBA constants that are part of the language environment rather than user
/// declarations. They are materialized as immutable module variables so the existing binder and
/// C# backend can reuse normal String constant semantics. User declarations keep precedence.
/// </summary>
internal static class VBBuiltInConstants
{
    private static readonly ImmutableArray<BoundModuleVariable> StringConstants =
        ImmutableArray.Create(
            CreateString("vbCrLf", "\r\n"),
            CreateString("vbCr", "\r"),
            CreateString("vbLf", "\n"),
            CreateString("vbNewLine", "\r\n"),
            CreateString("vbTab", "\t"),
            CreateString("vbBack", "\b"),
            CreateString("vbFormFeed", "\f"),
            CreateString("vbVerticalTab", "\v"),
            CreateString("vbNullChar", "\0"),
            CreateString("vbNullString", string.Empty));

    public static ImmutableArray<BoundModuleVariable> AddTo(
        IDictionary<string, ModuleVariableSymbol> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var visible = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        foreach (var constant in StringConstants)
        {
            if (variables.ContainsKey(constant.Symbol.Name))
            {
                continue;
            }

            variables.Add(constant.Symbol.Name, constant.Symbol);
            visible.Add(constant);
        }

        return visible.ToImmutable();
    }

    private static BoundModuleVariable CreateString(string name, string value)
    {
        var symbol = new ModuleVariableSymbol(name, TypeSymbol.String);
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(value, TypeSymbol.String),
            IsConstant: true);
    }
}
