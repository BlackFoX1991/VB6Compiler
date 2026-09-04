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

    /// <summary>
    /// Invokes, gets, or sets a member selected at runtime. The call-type values are the public
    /// VB6 constants vbMethod (1), vbGet (2), vbLet (4), and vbSet (8).
    /// </summary>
    public static object? CallByName(
        object? target,
        string procName,
        int callType,
        VBArray<object> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procName);
        ArgumentNullException.ThrowIfNull(arguments);

        return callType switch
        {
            1 => VBDynamicDispatch.InvokeMember(target, procName, arguments),
            2 => arguments.Length == 0
                ? VBDynamicDispatch.GetMember(target, procName)
                : VBDynamicDispatch.GetIndexedMember(target, procName, arguments),
            4 or 8 => SetCallByNameMember(target, procName, arguments),
            _ => throw new VB6RuntimeErrorException(5, $"Unsupported CallByName call type {callType}.")
        };
    }

    /// <summary>Returns the OLE_COLOR corresponding to one of the sixteen QBASIC colors.</summary>
    public static int QBColor(short color) => color switch
    {
        0 => 0x000000,
        1 => 0x800000,
        2 => 0x008000,
        3 => 0x808000,
        4 => 0x000080,
        5 => 0x800080,
        6 => 0x008080,
        7 => 0xC0C0C0,
        8 => 0x808080,
        9 => 0xFF0000,
        10 => 0x00FF00,
        11 => 0xFFFF00,
        12 => 0x0000FF,
        13 => 0xFF00FF,
        14 => 0x00FFFF,
        15 => 0xFFFFFF,
        _ => throw new VB6RuntimeErrorException(5, "QBColor requires a color number from 0 through 15.")
    };

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
            _ => Vb6TypeName(value.GetType())
        };
    }

    /// <summary>Returns the supplied values as a zero-based Variant array.</summary>
    /// <summary>
    /// Der Emitter praefixt jeden erzeugten Typ ("__vb6_class_Box"), damit VB6-Namen im
    /// gemeinsamen Namensraum nicht kollidieren. TypeName muss den VB6-Namen zurueckgeben,
    /// sonst wird das Namensschema des Emitters zu beobachtbarem Programmverhalten.
    ///
    /// Oeffentlich, weil ein Host dieselbe Frage hat: Der Fenstertitel und der Form.Name einer
    /// Form ohne Caption stammen von hier, und ohne diese Aufloesung stand im Titelbalken des
    /// VISIA-Splashfensters "__vb6_class_frmSplash".
    /// </summary>
    public static string Vb6TypeName(Type type)
    {
        var name = type.Name;
        foreach (var prefix in EmittedTypePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }

        return name;
    }

    private static readonly string[] EmittedTypePrefixes =
    [
        "__vb6_class_",
        "__vb6_interface_",
        "__vb6_udt_",
        "__vb6_module_"
    ];

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

    private static object? SetCallByNameMember(
        object? target,
        string procName,
        VBArray<object> arguments)
    {
        if (arguments.Length == 0)
        {
            throw new VB6RuntimeErrorException(5, "CallByName property assignment requires a value argument.");
        }

        var value = arguments[arguments.UBound()];
        if (arguments.Length == 1)
        {
            VBDynamicDispatch.SetMember(target, procName, value);
            return null;
        }

        var indexes = new VBArray<object>(new VBArrayBound(0, arguments.Length - 2));
        for (var index = 0; index < indexes.Length; index++)
        {
            indexes[index] = arguments[arguments.LBound() + index];
        }

        VBDynamicDispatch.SetIndexedMember(target, procName, indexes, value);
        return null;
    }

    private static int ClampColor(int value) => Math.Clamp(value, 0, 255);
}
