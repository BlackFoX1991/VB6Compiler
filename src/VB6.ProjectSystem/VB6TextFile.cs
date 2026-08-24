using System.Text;

namespace VB6.ProjectSystem;

/// <summary>Reads VB6 project and source files while preserving common legacy encodings.</summary>
public static class VB6TextFile
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static VB6TextFile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string ReadAllText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var bytes = File.ReadAllBytes(path);
        if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            return StrictUtf8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
        {
            return Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4);
        }

        if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
        {
            return new UTF32Encoding(bigEndian: true, byteOrderMark: false).GetString(bytes, 4, bytes.Length - 4);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // VB6 commonly saved Western-European projects in the Windows ANSI code page.
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static bool HasPrefix(byte[] bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (bytes[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }
}
