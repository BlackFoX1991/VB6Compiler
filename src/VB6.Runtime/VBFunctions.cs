namespace VB6.Runtime;

/// <summary>Small, backend-independent VB6 functions that operate on Variant values.</summary>
public static class VBFunctions
{
    public static string TypeName(object? value) => value switch
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
        VBDateValue => "Date",
        string => "String",
        VBArray<object> => "Variant()",
        _ => value.GetType().Name
    };

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

        return null;
    }
}
