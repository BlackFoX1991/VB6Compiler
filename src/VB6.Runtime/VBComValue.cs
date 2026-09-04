namespace VB6.Runtime;

/// <summary>
/// What a VB6 value looks like on the other side of a COM call.
///
/// Two VB6 types are represented here by a struct of this runtime rather than by a CLR primitive:
/// <see cref="VBCurrency"/> is a scaled Int64, and <see cref="VBDateValue"/> is an OLE date. Neither
/// can be put into a VARIANT as it stands -- the marshaller answers "cannot be marshalled to a
/// Variant. Type library is not registered", which reads like a registration problem and is not one.
///
/// Every path that hands a VB6 value to COM goes through here, so the two representations stay in
/// one place instead of being converted wherever a call happens to be written.
/// </summary>
internal static class VBComValue
{
    public static object? ToAutomation(object? value) => value switch
    {
        VBCurrency currency => currency.ToDecimal(),
        VBDateValue date => DateTime.FromOADate(date.OADate),
        _ => value
    };

    /// <summary>Applies <see cref="ToAutomation"/> to a whole argument list, in place.</summary>
    public static object?[] ToAutomation(object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = ToAutomation(arguments[index]);
        }

        return arguments;
    }
}
