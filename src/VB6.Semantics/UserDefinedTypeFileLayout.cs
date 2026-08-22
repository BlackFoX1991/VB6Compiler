namespace VB6.Semantics;

/// <summary>
/// Describes the subset of UDT layouts that can be transferred by VB6 binary Get/Put without
/// depending on CLR padding, references, or a native ABI. Fixed array members are expanded by the
/// IR lowerer; dynamic arrays and unsupported element layouts remain outside this contract.
/// </summary>
public static class UserDefinedTypeFileLayout
{
    public static bool IsBinaryTransferable(UserDefinedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return IsBinaryTransferable(type, new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance));
    }

    private static bool IsBinaryTransferable(
        UserDefinedTypeSymbol type,
        HashSet<UserDefinedTypeSymbol> activePath)
    {
        if (!activePath.Add(type) || type.Members.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var member in type.Members)
        {
            if (member.Type is ArrayTypeSymbol array)
            {
                if (!member.HasArrayBounds || !IsBinaryTransferableElement(array.ElementType, activePath))
                {
                    activePath.Remove(type);
                    return false;
                }

                continue;
            }

            if (member.Type is UserDefinedTypeSymbol nested &&
                !IsBinaryTransferable(nested, activePath))
            {
                activePath.Remove(type);
                return false;
            }

            if (member.Type is not UserDefinedTypeSymbol && !IsBinaryScalar(member.Type))
            {
                activePath.Remove(type);
                return false;
            }
        }

        activePath.Remove(type);
        return true;
    }

    private static bool IsBinaryTransferableElement(
        TypeSymbol type,
        HashSet<UserDefinedTypeSymbol> activePath) =>
        type is UserDefinedTypeSymbol nested
            ? IsBinaryTransferable(nested, activePath)
            : IsBinaryScalar(type);

    private static bool IsBinaryScalar(TypeSymbol type) =>
        type == TypeSymbol.Byte ||
        type == TypeSymbol.Integer ||
        type == TypeSymbol.Long ||
        type == TypeSymbol.LongLong ||
        type == TypeSymbol.Single ||
        type == TypeSymbol.Double ||
        type == TypeSymbol.Currency ||
        type == TypeSymbol.Boolean;
}
