using System.Globalization;

namespace VB6.Runtime;

public static class VBConversions
{
    public static short CInt(object? value) => Convert.ToInt16(value, CultureInfo.CurrentCulture);

    public static double CDbl(object? value) => Convert.ToDouble(value, CultureInfo.CurrentCulture);

    public static bool CBool(object? value) => Convert.ToBoolean(value, CultureInfo.CurrentCulture);

    public static string CStr(object? value) => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
}

public static class VBOperators
{
    public static short AddInteger(short left, short right) => checked((short)(left + right));

    public static short SubtractInteger(short left, short right) => checked((short)(left - right));

    public static short MultiplyInteger(short left, short right) => checked((short)(left * right));

    public static short NegateInteger(short value) => checked((short)-value);

    public static short IntegerDivide(short left, short right) => checked((short)(left / right));

    public static double DivideDouble(double left, double right) => left / right;

    public static string Concat(object? left, object? right) => VBConversions.CStr(left) + VBConversions.CStr(right);

    public static bool Equal(object? left, object? right) => Compare(left, right) == 0;

    public static bool NotEqual(object? left, object? right) => Compare(left, right) != 0;

    public static bool Less(object? left, object? right) => Compare(left, right) < 0;

    public static bool LessOrEqual(object? left, object? right) => Compare(left, right) <= 0;

    public static bool Greater(object? left, object? right) => Compare(left, right) > 0;

    public static bool GreaterOrEqual(object? left, object? right) => Compare(left, right) >= 0;

    private static int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is string leftString && right is string rightString)
        {
            return string.CompareOrdinal(leftString, rightString);
        }

        if (left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        throw new InvalidOperationException($"Values of type '{left.GetType()}' cannot be compared.");
    }
}

public static class VBDebug
{
    public static void Print(object? value) => Console.WriteLine(value);
}
