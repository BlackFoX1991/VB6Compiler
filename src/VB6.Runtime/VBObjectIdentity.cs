using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>Reference identity used by the VB6 <c>Is</c> operator.</summary>
public static class VBObjectIdentity
{
    /// <summary>
    /// Guards the places where VB6 demands an object expression: both operands of <c>Is</c> and the
    /// right-hand side of <c>Set</c>. A concrete object slot is checked by the type system, but a
    /// Variant only reveals what it carries at run time - and <c>Empty</c> is the CLR null
    /// reference, which would otherwise compare equal to <c>Nothing</c> and erase exactly the
    /// distinction this contract is about.
    /// </summary>
    public static object? RequireObjectOperand(object? value) =>
        VBVariants.IsObject(value)
            ? value
            : throw new VB6RuntimeErrorException(424, "An object reference is required here.");

    public static bool IsSame(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        // A Variant needs a marker for Nothing so it remains distinguishable from Empty.  A
        // concrete object reference has no such marker and uses null, but both values mean
        // Nothing to the VB6 Is operator.
        if (VBVariants.IsNothing(left))
        {
            left = null;
        }

        if (VBVariants.IsNothing(right))
        {
            right = null;
        }

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
