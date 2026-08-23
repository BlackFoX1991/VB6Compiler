using System.Collections.Immutable;
using VB6.Semantics;

namespace VB6.Compiler;

/// <summary>
/// Project-global VB/VBA constants that are part of the language environment rather than user
/// declarations. They are materialized as immutable module variables so the existing binder and
/// backends can reuse normal constant semantics. User declarations keep precedence.
/// </summary>
internal static class VBBuiltInConstants
{
    private static readonly ImmutableArray<BoundModuleVariable> Constants =
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
            CreateLong("vbAbort", 3),
            CreateLong("vbRetry", 4),
            CreateLong("vbIgnore", 5),
            CreateLong("vbDefaultButton1", 0),
            CreateLong("vbDefaultButton2", 256),
            CreateLong("vbDefaultButton3", 512),
            CreateLong("vbDefaultButton4", 768),
            CreateLong("vbApplicationModal", 0),
            CreateLong("vbSystemModal", 4096),
            CreateLong("vbMsgBoxHelp", 16384),
            CreateLong("vbMsgBoxSetForeground", 65536),
            CreateLong("vbMsgBoxRight", 524288),
            CreateLong("vbMsgBoxRtlReading", 1048576),
            CreateLong("vbTrue", -1),
            CreateLong("vbFalse", 0),
            CreateLong("vbEmpty", 0),
            CreateLong("vbNull", 1),
            CreateLong("vbInteger", 2),
            CreateLong("vbLong", 3),
            CreateLong("vbSingle", 4),
            CreateLong("vbDouble", 5),
            CreateLong("vbCurrency", 6),
            CreateLong("vbDate", 7),
            CreateLong("vbString", 8),
            CreateLong("vbObject", 9),
            CreateLong("vbError", 10),
            CreateLong("vbBoolean", 11),
            CreateLong("vbVariant", 12),
            CreateLong("vbDataObject", 13),
            CreateLong("vbDecimal", 14),
            CreateLong("vbByte", 17),
            CreateLong("vbUserDefinedType", 36),
            CreateLong("vbArray", 8192),
            CreateLong("vbObjectError", -2147221504),
            CreateLong("vbBlack", 0),
            CreateLong("vbRed", 255),
            CreateLong("vbGreen", 65280),
            CreateLong("vbYellow", 65535),
            CreateLong("vbBlue", 16711680),
            CreateLong("vbMagenta", 16711935),
            CreateLong("vbCyan", 16776960),
            CreateLong("vbWhite", 16777215),
            CreateLong("vbButtonFace", -2147483633),
            CreateLong("vbButtonShadow", -2147483632),
            CreateLong("vbGrayText", -2147483631),
            CreateLong("vbWindowBackground", -2147483643),
            CreateLong("vbWindowText", -2147483640),
            CreateLong("vbHighlight", -2147483635),
            CreateLong("vbHighlightText", -2147483634),
            CreateLong("vbActiveCaption", -2147483646),
            CreateLong("vbInactiveCaption", -2147483645),
            CreateLong("vbActiveCaptionText", -2147483639),
            CreateLong("vbInactiveCaptionText", -2147483629),
            CreateLong("vbScrollBars", -2147483648),
            CreateLong("vbHorizontal", 1),
            CreateLong("vbVertical", 2),
            CreateLong("vbBoth", 3),
            CreateLong("vbLeftButton", 1),
            CreateLong("vbRightButton", 2),
            CreateLong("vbMiddleButton", 4),
            CreateLong("vbDefault", 0),
            CreateLong("vbArrow", 0),
            CreateLong("vbCrosshair", 2),
            CreateLong("vbIBeam", 3),
            CreateLong("vbIconPointer", 4),
            CreateLong("vbSizePointer", 5),
            CreateLong("vbSizeNESW", 6),
            CreateLong("vbSizeNS", 7),
            CreateLong("vbSizeNWSE", 8),
            CreateLong("vbSizeWE", 9),
            CreateLong("vbUpArrow", 10),
            CreateLong("vbHourglass", 11),
            CreateLong("vbNoDrop", 12),
            CreateLong("vbArrowQuestion", 13),
            CreateLong("vbSizeAll", 14),
            CreateLong("vbCustom", 99),
            CreateLong("vbNormal", 0),
            CreateLong("vbMinimized", 1),
            CreateLong("vbMaximized", 2),
            CreateLong("vbHide", 0),
            CreateLong("vbNormalFocus", 1),
            CreateLong("vbMinimizedFocus", 2),
            CreateLong("vbMaximizedFocus", 3),
            CreateLong("vbNormalNoFocus", 4),
            CreateLong("vbMinimizedNoFocus", 6),
            CreateLong("vbSolid", 0),
            CreateLong("vbTransparent", 1),
            CreateLong("vbAltMask", 4),
            CreateLong("vbShiftMask", 1),
            CreateLong("vbCtrlMask", 2),
            CreateLong("vbKeyControl", 17),
            CreateLong("vbKeyShift", 16),
            CreateLong("vbKeyTab", 9),
            CreateLong("vbKeyUp", 38),
            CreateLong("vbKeyDown", 40),
            CreateLong("vbKeyLeft", 37),
            CreateLong("vbKeyRight", 39),
            CreateLong("vbKeyHome", 36),
            CreateLong("vbKeyEnd", 35),
            CreateLong("vbKeyPageUp", 33),
            CreateLong("vbKeyPageDown", 34),
            CreateLong("vbKeyC", 67),
            CreateLong("vbKeyE", 69),
            CreateLong("vbKeyF", 70),
            CreateLong("vbKeyH", 72),
            CreateLong("vbKeyS", 83),
            CreateLong("vbKeyV", 86),
            CreateLong("vbKeyX", 88),
            CreateLong("vbKeyY", 89),
            CreateLong("vbPicTypeBitmap", 0),
            CreateLong("vbPicTypeIcon", 1),
            CreateLong("vbPicTypeMetafile", 2),
            CreateLong("vbPicTypeEnhMetafile", 3),
            CreateLong("vbSrcCopy", 13369376),
            CreateLong("tvwChild", 4),
            CreateLong("BF_RECT", 15),
            CreateLong("EDGE_RAISED", 5),
            CreateObject("App", VBStandardTypes.App),
            CreateObject("Control", VBStandardTypes.Control),
            CreateObject("UserControl", VBStandardTypes.UserControl),
            CreateObject("Screen", VBStandardTypes.Screen),
            CreateObject("Ambient", VBStandardTypes.Ambient),
            CreateObject("Clipboard", VBStandardTypes.Clipboard),
            CreateVariant("Err"));

    public static ImmutableArray<BoundModuleVariable> AddTo(
        IDictionary<string, ModuleVariableSymbol> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var visible = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        foreach (var constant in Constants)
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
        var symbol = new ModuleVariableSymbol(name, TypeSymbol.String)
        {
            IsConstant = true
        };
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(value, TypeSymbol.String),
            IsConstant: true);
    }

    private static BoundModuleVariable CreateLong(string name, int value)
    {
        var symbol = new ModuleVariableSymbol(name, TypeSymbol.Long)
        {
            IsConstant = true
        };
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(value, TypeSymbol.Long),
            IsConstant: true);
    }

    private static BoundModuleVariable CreateVariant(string name)
    {
        var symbol = new ModuleVariableSymbol(name, TypeSymbol.Variant)
        {
            IsConstant = true
        };
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(null, TypeSymbol.Variant),
            IsConstant: true);
    }

    private static BoundModuleVariable CreateObject(string name, ClassTypeSymbol type)
    {
        var symbol = new ModuleVariableSymbol(name, type)
        {
            IsConstant = true
        };
        return new BoundModuleVariable(
            symbol,
            new BoundLiteralExpression(null, type),
            IsConstant: true);
    }
}
