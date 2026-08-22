using System.Globalization;
using System.Text;

namespace VB6.Runtime;

/// <summary>
/// VB6 file I/O addressed by file number.
///
/// File numbers are a process-wide table in VB6, not handles the program carries around, so the
/// same table shape is kept here. Positions are one-based byte offsets: <c>Get #1, 1, b</c> reads
/// the first byte of the file. Omitting the position continues where the previous operation
/// stopped, which is why the core operations take a nullable position.
///
/// Fixed-size numeric types, variable-length binary Strings, and basic text output are supported.
/// A binary String is stored with a two-byte character-count prefix followed by UTF-16LE
/// characters, matching the BSTR-oriented VB6 transfer contract. User-defined types still require
/// an explicit record layout.
/// </summary>
public static class VBFiles
{
    private static IEnumerator<string>? _directoryEnumerator;

    /// <summary>Deletes one filesystem path using the VB6 Kill contract.</summary>
    public static void Kill(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.Delete(path);
    }

    /// <summary>
    /// Implements Dir's stateful first-call/continuation form. Attributes are accepted for source
    /// compatibility; the portable profile currently enumerates ordinary files only.
    /// </summary>
    public static string Dir(string path, int attributes)
    {
        if (!string.IsNullOrEmpty(path))
        {
            var directory = Path.GetDirectoryName(path);
            var pattern = Path.GetFileName(path);
            directory = string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
            _directoryEnumerator = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, string.IsNullOrEmpty(pattern) ? "*" : pattern).GetEnumerator()
                : null;
        }

        if (_directoryEnumerator is null || !_directoryEnumerator.MoveNext())
        {
            _directoryEnumerator?.Dispose();
            _directoryEnumerator = null;
            return string.Empty;
        }

        return Path.GetFileName(_directoryEnumerator.Current);
    }

    public static long Length(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileInfo(path).Length;
    }

    private const int MinimumFileNumber = 1;
    private const int MaximumFileNumber = 511;

    private static readonly Dictionary<int, FileStream> OpenFiles = new();

    public static void OpenBinary(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
    }

    public static void OpenInput(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.Open, FileAccess.Read);
    }

    public static void OpenOutput(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.Create, FileAccess.Write);
    }

    public static void OpenAppend(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.Append, FileAccess.Write);
    }

    public static void Print(int fileNumber, object? value)
    {
        var stream = GetStream(fileNumber);
        var bytes = Encoding.UTF8.GetBytes(VBDebug.Format(value) + "\r\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    public static string LineInput(int fileNumber)
    {
        var stream = GetStream(fileNumber);
        SkipUtf8Bom(stream);
        var bytes = new List<byte>();
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                if (bytes.Count == 0)
                {
                    throw new EndOfStreamException("Line Input reached the end of the file.");
                }

                break;
            }

            if (value == '\n')
            {
                break;
            }

            bytes.Add((byte)value);
        }

        if (bytes.Count > 0 && bytes[^1] == '\r')
        {
            bytes.RemoveAt(bytes.Count - 1);
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public static string InputField(int fileNumber)
    {
        var stream = GetStream(fileNumber);
        SkipUtf8Bom(stream);
        var bytes = new List<byte>();
        var quoted = false;
        var sawValue = false;

        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                if (!sawValue && bytes.Count == 0)
                {
                    throw new EndOfStreamException("Input # reached the end of the file.");
                }

                break;
            }

            sawValue = true;
            if (quoted)
            {
                if (value != '"')
                {
                    bytes.Add((byte)value);
                    continue;
                }

                var next = stream.ReadByte();
                if (next == '"')
                {
                    bytes.Add((byte)'"');
                    continue;
                }

                quoted = false;
                if (next < 0 || next is ',' or '\n')
                {
                    break;
                }

                if (next == '\r')
                {
                    ConsumeLineFeed(stream);
                    break;
                }

                bytes.Add((byte)next);
                continue;
            }

            if (value == '"' && bytes.Count == 0)
            {
                quoted = true;
            }
            else if (value is ',' or '\n')
            {
                break;
            }
            else if (value == '\r')
            {
                ConsumeLineFeed(stream);
                break;
            }
            else
            {
                bytes.Add((byte)value);
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static void ConsumeLineFeed(FileStream stream)
    {
        var next = stream.ReadByte();
        if (next >= 0 && next != '\n')
        {
            stream.Position--;
        }
    }

    private static void SkipUtf8Bom(FileStream stream)
    {
        if (stream.Position != 0)
        {
            return;
        }

        var first = stream.ReadByte();
        var second = stream.ReadByte();
        var third = stream.ReadByte();
        if (first == 0xEF && second == 0xBB && third == 0xBF)
        {
            return;
        }

        stream.Position = 0;
    }

    private static void OpenFile(int fileNumber, string path, FileMode mode, FileAccess access)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidateFileNumber(fileNumber);

        if (OpenFiles.ContainsKey(fileNumber))
        {
            throw new InvalidOperationException(
                $"VB6 file number {fileNumber.ToString(CultureInfo.InvariantCulture)} is already open.");
        }

        OpenFiles.Add(fileNumber, new FileStream(
            path,
            mode,
            access,
            FileShare.ReadWrite));
    }

    public static void Close(int fileNumber)
    {
        ValidateFileNumber(fileNumber);
        if (!OpenFiles.Remove(fileNumber, out var stream))
        {
            return;
        }

        stream.Dispose();
    }

    public static void CloseAll()
    {
        foreach (var stream in OpenFiles.Values)
        {
            stream.Dispose();
        }

        OpenFiles.Clear();
    }

    public static void Seek(int fileNumber, long position)
    {
        var stream = GetStream(fileNumber);
        if (position < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                "VB6 file positions are one-based.");
        }

        stream.Position = position - 1;
    }

    public static long Position(int fileNumber) => GetStream(fileNumber).Position + 1;

    public static long Length(int fileNumber) => GetStream(fileNumber).Length;

    public static int FreeFile()
    {
        for (var candidate = MinimumFileNumber; candidate <= MaximumFileNumber; candidate++)
        {
            if (!OpenFiles.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No VB6 file number is available.");
    }

    public static bool EndOfFile(int fileNumber)
    {
        var stream = GetStream(fileNumber);
        return stream.Position >= stream.Length;
    }

    public static byte GetByte(int fileNumber, long? position) => Read(fileNumber, position, 1)[0];

    public static short GetInteger(int fileNumber, long? position) =>
        BitConverter.ToInt16(Read(fileNumber, position, 2));

    public static int GetLong(int fileNumber, long? position) =>
        BitConverter.ToInt32(Read(fileNumber, position, 4));

    public static long GetLongLong(int fileNumber, long? position) =>
        BitConverter.ToInt64(Read(fileNumber, position, 8));

    public static float GetSingle(int fileNumber, long? position) =>
        BitConverter.ToSingle(Read(fileNumber, position, 4));

    public static double GetDouble(int fileNumber, long? position) =>
        BitConverter.ToDouble(Read(fileNumber, position, 8));

    public static VBCurrency GetCurrency(int fileNumber, long? position) =>
        VBCurrency.FromScaled(BitConverter.ToInt64(Read(fileNumber, position, 8)));

    public static bool GetBoolean(int fileNumber, long? position) =>
        BitConverter.ToInt16(Read(fileNumber, position, 2)) != 0;

    public static string GetString(int fileNumber, long? position)
    {
        var characterCount = BitConverter.ToUInt16(Read(fileNumber, position, 2));
        if (characterCount == 0)
        {
            return string.Empty;
        }

        var bytes = Read(fileNumber, null, checked(characterCount * sizeof(char)));
        return Encoding.Unicode.GetString(bytes);
    }

    // The direct managed backend uses ordinary CLI signatures rather than constructing Nullable<T>
    // just to express an omitted record position. These overloads preserve the same core behavior.
    public static byte GetByte(int fileNumber) => GetByte(fileNumber, null);
    public static byte GetByte(int fileNumber, long position) => GetByte(fileNumber, (long?)position);
    public static short GetInteger(int fileNumber) => GetInteger(fileNumber, null);
    public static short GetInteger(int fileNumber, long position) => GetInteger(fileNumber, (long?)position);
    public static int GetLong(int fileNumber) => GetLong(fileNumber, null);
    public static int GetLong(int fileNumber, long position) => GetLong(fileNumber, (long?)position);
    public static long GetLongLong(int fileNumber) => GetLongLong(fileNumber, null);
    public static long GetLongLong(int fileNumber, long position) => GetLongLong(fileNumber, (long?)position);
    public static float GetSingle(int fileNumber) => GetSingle(fileNumber, null);
    public static float GetSingle(int fileNumber, long position) => GetSingle(fileNumber, (long?)position);
    public static double GetDouble(int fileNumber) => GetDouble(fileNumber, null);
    public static double GetDouble(int fileNumber, long position) => GetDouble(fileNumber, (long?)position);
    public static VBCurrency GetCurrency(int fileNumber) => GetCurrency(fileNumber, null);
    public static VBCurrency GetCurrency(int fileNumber, long position) => GetCurrency(fileNumber, (long?)position);
    public static bool GetBoolean(int fileNumber) => GetBoolean(fileNumber, null);
    public static bool GetBoolean(int fileNumber, long position) => GetBoolean(fileNumber, (long?)position);
    public static string GetString(int fileNumber) => GetString(fileNumber, null);
    public static string GetString(int fileNumber, long position) => GetString(fileNumber, (long?)position);

    public static void Put(int fileNumber, long? position, byte value) =>
        Write(fileNumber, position, new[] { value });

    public static void Put(int fileNumber, long? position, short value) =>
        Write(fileNumber, position, BitConverter.GetBytes(value));

    public static void Put(int fileNumber, long? position, int value) =>
        Write(fileNumber, position, BitConverter.GetBytes(value));

    public static void Put(int fileNumber, long? position, long value) =>
        Write(fileNumber, position, BitConverter.GetBytes(value));

    public static void Put(int fileNumber, long? position, float value) =>
        Write(fileNumber, position, BitConverter.GetBytes(value));

    public static void Put(int fileNumber, long? position, double value) =>
        Write(fileNumber, position, BitConverter.GetBytes(value));

    public static void Put(int fileNumber, long? position, VBCurrency value) =>
        Write(fileNumber, position, BitConverter.GetBytes(value.ScaledValue));

    public static void Put(int fileNumber, long? position, bool value) =>
        Write(fileNumber, position, BitConverter.GetBytes(value ? (short)-1 : (short)0));

    public static void Put(int fileNumber, long? position, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > ushort.MaxValue)
        {
            throw new OverflowException("VB6 binary String transfers support at most 65535 characters.");
        }

        var payload = Encoding.Unicode.GetBytes(value);
        var bytes = new byte[sizeof(ushort) + payload.Length];
        BitConverter.GetBytes((ushort)value.Length).CopyTo(bytes, 0);
        payload.CopyTo(bytes, sizeof(ushort));
        Write(fileNumber, position, bytes);
    }

    public static void Put(int fileNumber, byte value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, byte value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, short value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, short value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, int value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, int value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, long value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, long value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, float value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, float value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, double value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, double value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, VBCurrency value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, VBCurrency value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, bool value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, bool value) => Put(fileNumber, (long?)position, value);
    public static void Put(int fileNumber, string value) => Put(fileNumber, null, value);
    public static void Put(int fileNumber, long position, string value) => Put(fileNumber, (long?)position, value);

    private static byte[] Read(int fileNumber, long? position, int count)
    {
        var stream = Seek(fileNumber, position);
        var buffer = new byte[count];
        var read = stream.ReadAtLeast(buffer, count, throwOnEndOfStream: false);
        if (read < count)
        {
            throw new EndOfStreamException(
                $"VB6 file number {fileNumber.ToString(CultureInfo.InvariantCulture)} has fewer than " +
                $"{count.ToString(CultureInfo.InvariantCulture)} bytes left to read.");
        }

        return buffer;
    }

    private static void Write(int fileNumber, long? position, byte[] bytes)
    {
        var stream = Seek(fileNumber, position);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static FileStream Seek(int fileNumber, long? position)
    {
        var stream = GetStream(fileNumber);
        if (position is not null)
        {
            Seek(fileNumber, position.Value);
        }

        return stream;
    }

    private static FileStream GetStream(int fileNumber)
    {
        ValidateFileNumber(fileNumber);
        if (!OpenFiles.TryGetValue(fileNumber, out var stream))
        {
            throw new InvalidOperationException(
                $"VB6 file number {fileNumber.ToString(CultureInfo.InvariantCulture)} is not open.");
        }

        return stream;
    }

    private static void ValidateFileNumber(int fileNumber)
    {
        if (fileNumber is < MinimumFileNumber or > MaximumFileNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileNumber),
                $"VB6 file numbers run from {MinimumFileNumber.ToString(CultureInfo.InvariantCulture)} " +
                $"to {MaximumFileNumber.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}
