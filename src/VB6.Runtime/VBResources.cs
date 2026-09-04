using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace VB6.Runtime;

/// <summary>
/// Reads the Win32 <c>.res</c> a VB6 project names with <c>ResFile32=</c>.
///
/// The format is a flat sequence of entries, each a header followed by the payload, and every entry
/// is aligned to four bytes — both the header and the data. A type or name is either an ordinal
/// (a <c>0xFFFF</c> marker followed by the number) or a zero-terminated UTF-16 string. The first
/// entry of a well-formed file is a zero-length placeholder and carries no payload.
///
/// Strings are the case that needs care: VB6's <c>LoadResString(id)</c> does not address a string
/// resource directly. Win32 stores strings in blocks of sixteen, and the block id is
/// <c>id \ 16 + 1</c> while the position inside it is <c>id Mod 16</c>. Reading a block as if it
/// were one string returns the whole table.
/// </summary>
public static class VBResources
{
    private const int TypeString = 6;

    /// <summary>Reads every entry of a .res file. A malformed tail stops the scan.</summary>
    public static ImmutableArray<VBResourceEntry> Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var entries = ImmutableArray.CreateBuilder<VBResourceEntry>();
        var offset = 0;
        while (offset + 8 <= bytes.Length)
        {
            var dataSize = BitConverter.ToInt32(bytes, offset);
            var headerSize = BitConverter.ToInt32(bytes, offset + 4);
            if (headerSize < 8 || offset + headerSize > bytes.Length || dataSize < 0)
            {
                break;
            }

            var cursor = offset + 8;
            if (!TryReadIdentifier(bytes, ref cursor, offset + headerSize, out var type) ||
                !TryReadIdentifier(bytes, ref cursor, offset + headerSize, out var name))
            {
                break;
            }

            var dataStart = offset + headerSize;
            if (dataStart + dataSize > bytes.Length)
            {
                break;
            }

            // The leading placeholder entry carries no payload and is not a resource.
            if (dataSize > 0)
            {
                entries.Add(new VBResourceEntry(type, name, bytes[dataStart..(dataStart + dataSize)]));
            }

            offset = Align(dataStart + dataSize);
        }

        return entries.ToImmutable();
    }

    /// <summary>
    /// The string with the supplied VB6 identifier, or null when the file does not carry it.
    /// </summary>
    public static string? FindString(ImmutableArray<VBResourceEntry> entries, int identifier)
    {
        if (identifier < 0)
        {
            return null;
        }

        var blockId = (identifier / 16) + 1;
        var index = identifier % 16;
        var block = entries.FirstOrDefault(entry =>
            entry.Type is { Ordinal: TypeString } && entry.Name is { Ordinal: var id } && id == blockId);
        if (block is null)
        {
            return null;
        }

        var data = block.Data;
        var offset = 0;
        for (var position = 0; position < 16; position++)
        {
            if (offset + 2 > data.Length)
            {
                return null;
            }

            var length = BitConverter.ToUInt16(data, offset);
            offset += 2;
            if (position == index)
            {
                return offset + (length * 2) > data.Length
                    ? null
                    : Encoding.Unicode.GetString(data, offset, length * 2);
            }

            offset += length * 2;
        }

        return null;
    }

    /// <summary>The payload of any non-string resource, addressed the way VB6 addresses it.</summary>
    public static byte[]? FindData(ImmutableArray<VBResourceEntry> entries, int identifier, int type) =>
        entries.FirstOrDefault(entry =>
            entry.Type is { Ordinal: var entryType } && entryType == type &&
            entry.Name is { Ordinal: var entryName } && entryName == identifier)?.Data;

    private static int Align(int value) => (value + 3) & ~3;

    private static bool TryReadIdentifier(byte[] bytes, ref int cursor, int limit, out VBResourceIdentifier identifier)
    {
        identifier = new VBResourceIdentifier(null, null);
        if (cursor + 2 > limit)
        {
            return false;
        }

        var first = BitConverter.ToUInt16(bytes, cursor);
        if (first == 0xFFFF)
        {
            if (cursor + 4 > limit)
            {
                return false;
            }

            identifier = new VBResourceIdentifier(BitConverter.ToUInt16(bytes, cursor + 2), null);
            cursor += 4;
            return true;
        }

        var builder = new StringBuilder();
        while (cursor + 2 <= limit)
        {
            var character = BitConverter.ToUInt16(bytes, cursor);
            cursor += 2;
            if (character == 0)
            {
                identifier = new VBResourceIdentifier(null, builder.ToString());
                return true;
            }

            builder.Append((char)character);
        }

        return false;
    }
}

/// <summary>A resource type or name: either an ordinal or a name, never both.</summary>
public sealed record VBResourceIdentifier(int? Ordinal, string? Name);

public sealed record VBResourceEntry(
    VBResourceIdentifier Type,
    VBResourceIdentifier Name,
    byte[] Data);


/// <summary>
/// The VB6 resource intrinsics. VB6 links the project's <c>.res</c> into the executable; the
/// emitter embeds the same bytes as a managed resource, so the lookups here read from the running
/// assembly rather than from a file that would have to ship beside it.
/// </summary>
public static class VBResourceIntrinsics
{
    /// <summary>The name the emitter gives the embedded copy of the project resource file.</summary>
    public const string EmbeddedResourceName = "VB6.Resources";

    private static ImmutableArray<VBResourceEntry>? _entries;

    /// <summary>Replaces the resource set. Used by tests; a program never calls it.</summary>
    public static void SetEntries(ImmutableArray<VBResourceEntry> entries) => _entries = entries;

    public static string LoadResString(int identifier)
    {
        var value = VBResources.FindString(Entries(), identifier);
        if (value is null)
        {
            // VB6 answers a resource that is not in the file with 326, not with an empty string.
            VBErrors.Raise(
                326,
                "LoadResString",
                "Resource with identifier not found",
                string.Empty,
                0);
        }

        return value!;
    }

    public static VBArray<byte> LoadResData(int identifier, object? resourceType)
    {
        var type = VBConversions.CLng(resourceType);
        var data = VBResources.FindData(Entries(), identifier, (int)type);
        if (data is null)
        {
            VBErrors.Raise(
                326,
                "LoadResData",
                "Resource with identifier not found",
                string.Empty,
                0);
            data = [];
        }

        var result = new VBArray<byte>(new VBArrayBound(0, data.Length - 1));
        for (var index = 0; index < data.Length; index++)
        {
            result[index] = data[index];
        }

        return result;
    }

    /// <summary>
    /// VB6 answers a picture object here. The host owns picture objects, so the bytes are handed to
    /// it; without a host there is nothing that could build one, and that is reported rather than
    /// answered with Nothing.
    /// </summary>
    public static object LoadResPicture(int identifier, object? resourceType)
    {
        var data = LoadResData(identifier, resourceType);
        var picture = VBInteraction.CreatePictureFromResource(data);
        if (picture is null)
        {
            VBErrors.Raise(
                481,
                "LoadResPicture",
                "Invalid picture",
                string.Empty,
                0);
        }

        return picture!;
    }

    private static ImmutableArray<VBResourceEntry> Entries()
    {
        if (_entries is { } cached)
        {
            return cached;
        }

        var assembly = Assembly.GetEntryAssembly();
        using var stream = assembly?.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null)
        {
            _entries = ImmutableArray<VBResourceEntry>.Empty;
            return _entries.Value;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        _entries = VBResources.Read(buffer.ToArray());
        return _entries.Value;
    }
}
