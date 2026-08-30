namespace VB6.Runtime;

/// <summary>Small, backend-independent VB6 functions that operate on Variant values.</summary>
public static class VBFunctions
{
    /// <summary>
    /// VB6 evaluates both value arguments before entering IIf. The compiler's ordinary call
    /// lowering preserves that eager evaluation; this method only selects the resulting value.
    /// </summary>
    public static object? IIf(bool condition, object? truePart, object? falsePart) =>
        condition ? truePart : falsePart;

    /// <summary>Builds a Windows OLE_COLOR value from RGB components, clamping each component.</summary>
    public static int RGB(int red, int green, int blue) =>
        ClampColor(red) |
        (ClampColor(green) << 8) |
        (ClampColor(blue) << 16);

    public static string TypeName(object? value)
    {
        if (VBVariants.IsNull(value))
        {
            return "Null";
        }

        if (VBVariants.IsNothing(value))
        {
            return "Nothing";
        }

        if (VBVariants.IsMissing(value))
        {
            return "Error";
        }

        return value switch
        {
            null => "Empty",
            VB6RaisedError => "VB6RaisedError",
            bool => "Boolean",
            byte => "Byte",
            short => "Integer",
            int => "Long",
            long => "LongLong",
            float => "Single",
            double => "Double",
            decimal => "Decimal",
            VBCurrency => "Currency",
            VBDateValue or DateTime => "Date",
            VBErrorValue => "Error",
            string => "String",
            // VB6 nennt das Standardobjekt "Collection"; der CLR-Typname der Runtime
            // waere hier nach aussen sichtbar.
            VBCollection => "Collection",
            IVBArray array => VBVariants.ArrayTypeName(array),
            Array array => VBVariants.ArrayTypeName(array),
            _ => value.GetType().Name
        };
    }

    /// <summary>Returns the supplied values as a zero-based Variant array.</summary>
    public static object Array(VBArray<object> arguments) => arguments;

    /// <summary>Evaluates condition/value pairs and returns the first matching value.</summary>
    public static object? Switch(VBArray<object> arguments)
    {
        if (arguments.Length % 2 != 0)
        {
            throw new ArgumentException("VB6 Switch requires condition/value pairs.", nameof(arguments));
        }

        for (var index = arguments.LBound(); index <= arguments.UBound(); index += 2)
        {
            if (VBVariants.ToBoolean(arguments[index]))
            {
                return arguments[index + 1];
            }
        }

        return VBVariants.NullValue();
    }

    /// <summary>Returns the one-based choice selected by the rounded index, or Variant Null.</summary>
    public static object? Choose(int index, VBArray<object> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (index < 1 || index > choices.Length)
        {
            return VBVariants.NullValue();
        }

        return choices[choices.LBound() + index - 1];
    }

    private static int ClampColor(int value) => Math.Clamp(value, 0, 255);
}
