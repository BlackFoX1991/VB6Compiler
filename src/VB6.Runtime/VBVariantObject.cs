using System.Reflection;
using System.Runtime.InteropServices;

namespace VB6.Runtime;

internal static class VBVariantObject
{
    private const int MaxDefaultPropertyDepth = 8;

    internal static object? ResolveDefaultValue(object? value)
    {
        var current = value;
        for (var depth = 0; depth < MaxDefaultPropertyDepth; depth++)
        {
            if (current is null || VBVariants.IsNothing(current) || !VBVariants.IsObject(current))
            {
                return current;
            }

            if (!TryGetDefaultValue(current, out var defaultValue))
            {
                return current;
            }

            if (ReferenceEquals(current, defaultValue))
            {
                return current;
            }

            current = defaultValue;
        }

        throw new VB6TypeMismatchException(
            "The Variant default-property chain is deeper than the supported limit.");
    }

    internal static bool TryGetDefaultValue(object? value, out object? defaultValue)
    {
        defaultValue = null;
        if (value is null || VBVariants.IsNothing(value) || !VBVariants.IsObject(value))
        {
            return false;
        }

        try
        {
            defaultValue = VBDynamicDispatch.GetDefaultMember(value, Array.Empty<object?>());
            return true;
        }
        catch (MissingMemberException)
        {
            return false;
        }
        catch (TargetParameterCountException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (COMException exception) when (IsMissingComMember(exception))
        {
            return false;
        }
        catch (TargetInvocationException exception) when (IsNoDefaultPropertyException(exception.InnerException))
        {
            return false;
        }
    }

    private static bool IsNoDefaultPropertyException(Exception? exception) => exception switch
    {
        MissingMemberException or TargetParameterCountException or InvalidCastException or ArgumentException => true,
        COMException comException => IsMissingComMember(comException),
        _ => false
    };

    private static bool IsMissingComMember(COMException exception) =>
        exception.ErrorCode is unchecked((int)0x80020003) or unchecked((int)0x80020006);
}
