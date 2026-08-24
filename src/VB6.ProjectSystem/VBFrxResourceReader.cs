using System.Buffers.Binary;

namespace VB6.ProjectSystem;

/// <summary>
/// Reads the length-prefixed payload stored at a designer resource offset in a VB6 <c>.frx</c>
/// file. The payload remains opaque here; control-specific decoders belong to the host layer.
/// </summary>
public static class VBFrxResourceReader
{
    public static byte[] Read(string filePath, int offset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        var bytes = File.ReadAllBytes(Path.GetFullPath(filePath));
        if (offset > bytes.Length - sizeof(uint))
        {
            throw new InvalidDataException(
                $"The .frx resource offset {offset:X} does not contain a length prefix.");
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
        if (length > int.MaxValue || length > bytes.Length - offset - sizeof(uint))
        {
            throw new InvalidDataException(
                $"The .frx resource at offset {offset:X} exceeds the file boundary.");
        }

        return bytes.AsSpan(offset + sizeof(uint), checked((int)length)).ToArray();
    }
}
