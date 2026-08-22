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
            CreateString("vbNullString", string.Empty),
            CreateLong("vbBinaryCompare", 0),
            CreateLong("vbTextCompare", 1),
            CreateLong("vbUseCompareOption", -1),
            CreateLong("vbUpperCase", 1),
            CreateLong("vbLowerCase", 2),
            CreateLong("vbProperCase", 3),
            CreateLong("vbWide", 4),
            CreateLong("vbNarrow", 8),
            CreateLong("vbKatakana", 16),
            CreateLong("vbHiragana", 32),
            CreateLong("vbUnicode", 64),
            CreateLong("vbFromUnicode", 128),
            CreateLong("vbOKOnly", 0),
            CreateLong("vbOKCancel", 1),
            CreateLong("vbAbortRetryIgnore", 2),
            CreateLong("vbYesNoCancel", 3),
            CreateLong("vbYesNo", 4),
            CreateLong("vbRetryCancel", 5),
            CreateLong("vbCritical", 16),
            CreateLong("vbQuestion", 32),
            CreateLong("vbExclamation", 48),
            CreateLong("vbInformation", 64),
            CreateLong("vbYes", 6),
            CreateLong("vbNo", 7),
            CreateLong("vbCancel", 2),
            CreateObject("App", VBStandardTypes.App),
            CreateObject("Control", VBStandardTypes.Control),
            CreateObject("UserControl", VBStandardTypes.UserControl),
            CreateVariant("Err"));

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

    private static BoundModuleVariable CreateLong(string name, int value)
    {
        var symbol = new ModuleVariableSymbol(name, TypeSymbol.Long);
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(value, TypeSymbol.Long),
            IsConstant: true);
    }

    private static BoundModuleVariable CreateVariant(string name)
    {
        var symbol = new ModuleVariableSymbol(name, TypeSymbol.Variant);
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(null, TypeSymbol.Variant),
            IsConstant: true);
    }

    private static BoundModuleVariable CreateObject(string name, ClassTypeSymbol type)
    {
        var symbol = new ModuleVariableSymbol(name, type);
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(null, type),
            IsConstant: true);
    }
}
