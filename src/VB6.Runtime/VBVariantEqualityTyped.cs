namespace VB6.Runtime;

public static partial class VBOperators
{
    public static bool Equal(byte left, byte right) => left == right;
    public static bool Equal(short left, short right) => left == right;
    public static bool Equal(int left, int right) => left == right;
    public static bool Equal(long left, long right) => left == right;
    public static bool Equal(float left, float right) => left.CompareTo(right) == 0;
    public static bool Equal(double left, double right) => left.CompareTo(right) == 0;
    public static bool Equal(bool left, bool right) => left == right;
    public static bool Equal(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
    public static bool Equal(VBCurrency left, VBCurrency right) => left.CompareTo(right) == 0;
}
