using System.Text;

namespace VB6.ProjectSystem;

public static class VB6SourceReader
{
    private static readonly char[] Windows1252Controls =
    [
        '\u20ac', '\u0081', '\u201a', '\u0192',
        '\u201e', '\u2026', '\u2020', '\u2021',
        '\u02c6', '\u2030', '\u0160', '\u2039',
        '\u0152', '\u008d', '\u017d', '\u008f',
        '\u0090', '\u2018', '\u2019', '\u201c',
        '\u201d', '\u2022', '\u2013', '\u2014',
        '\u02dc', '\u2122', '\u0161', '\u203a',
        '\u0153', '\u009d', '\u017e', '\u0178'
    ];

    public static string ReadAllText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Decode(File.ReadAllBytes(path));
    }

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
        {
            return Encoding.UTF32.GetString(bytes[4..]);
        }

        if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
        {
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true).GetString(bytes[4..]);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            return Encoding.Unicode.GetString(bytes[2..]);
        }

        if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            return Encoding.BigEndianUnicode.GetString(bytes[2..]);
        }

        return DecodeWindows1252(bytes);
    }

    private static bool HasPrefix(ReadOnlySpan<byte> bytes, params byte[] prefix) =>
        bytes.Length >= prefix.Length && bytes[..prefix.Length].SequenceEqual(prefix);

    private static string DecodeWindows1252(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            builder.Append(value is >= 0x80 and <= 0x9F
                ? Windows1252Controls[value - 0x80]
                : (char)value);
        }

        return builder.ToString();
    }
}
