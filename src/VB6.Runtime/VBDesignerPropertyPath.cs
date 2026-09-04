namespace VB6.Runtime;

/// <summary>
/// A designer property name from a <c>.frm</c>/<c>.ctl</c> envelope. VB6 nests them with
/// <c>BeginProperty</c>, and the nesting arrives here as a dotted path — an ImageList entry is
/// <c>Images.ListImage1.Picture</c>, a font is <c>Font.Name</c>.
///
/// The names travel through <see cref="IVB6Host.TrySetMember"/>, which is why reading them belongs
/// to the host contract rather than to the compiler or to one host implementation: the compiler
/// decides which of them to emit, and the host has to recognise the same shape.
/// </summary>
public static class VBDesignerPropertyPath
{
    /// <summary>The last segment — the property itself, without the groups that contain it.</summary>
    public static string Leaf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var separator = name.LastIndexOf('.');
        return separator < 0 ? name : name[(separator + 1)..];
    }

    /// <summary>
    /// Reads an ImageList entry, wherever the group path puts it. VB6 writes the collection name
    /// in front of the entry, and matching from the start of the name would tie the reader to one
    /// particular nesting depth.
    /// </summary>
    public static bool TryReadListImageEntry(string name, out int index, out string property)
    {
        ArgumentNullException.ThrowIfNull(name);
        const string prefix = "ListImage";

        index = 0;
        property = string.Empty;

        var leafSeparator = name.LastIndexOf('.');
        if (leafSeparator < 0)
        {
            return false;
        }

        var leaf = name[(leafSeparator + 1)..];
        if (!leaf.Equals("Picture", StringComparison.OrdinalIgnoreCase) &&
            !leaf.Equals("Key", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var entryStart = name.LastIndexOf('.', leafSeparator - 1) + 1;
        var entry = name[entryStart..leafSeparator];
        if (entry.Length <= prefix.Length ||
            !entry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(
                entry.AsSpan(prefix.Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed <= 0)
        {
            return false;
        }

        index = parsed;
        property = leaf;
        return true;
    }
}
