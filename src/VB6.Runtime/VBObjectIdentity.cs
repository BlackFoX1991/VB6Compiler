using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>Reference identity used by the VB6 <c>Is</c> operator.</summary>
public static class VBObjectIdentity
{
    public static bool IsSame(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null ||
            !OperatingSystem.IsWindows() ||
            !Marshal.IsComObject(left) ||
            !Marshal.IsComObject(right))
        {
            return false;
        }

        nint leftUnknown = IntPtr.Zero;
        nint rightUnknown = IntPtr.Zero;
        try
        {
            leftUnknown = Marshal.GetIUnknownForObject(left);
            rightUnknown = Marshal.GetIUnknownForObject(right);
            return leftUnknown == rightUnknown;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            if (leftUnknown != IntPtr.Zero)
            {
                Marshal.Release(leftUnknown);
            }

            if (rightUnknown != IntPtr.Zero)
            {
                Marshal.Release(rightUnknown);
            }
        }
    }

    public static bool IsType(object? value, Type targetType) =>
        value is not null && targetType.IsInstanceOfType(value);
}
