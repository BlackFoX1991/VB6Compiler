using System.Globalization;

namespace VB6.Runtime;

/// <summary>
/// VB6 binary file I/O addressed by file number.
///
/// File numbers are a process-wide table in VB6, not handles the program carries around, so the
/// same table shape is kept here. Positions are one-based byte offsets: <c>Get #1, 1, b</c> reads
/// the first byte of the file. Omitting the position continues where the previous operation
/// stopped, which is why the core operations take a nullable position.
///
/// Only the fixed-size numeric types are supported. A variable-length String is stored with a
/// two-byte length prefix and a user-defined type is written in its record layout; both need rules
/// this runtime does not model yet, and the compiler reports them instead of guessing.
/// </summary>
public static class VBFiles
{
    private const int MinimumFileNumber = 1;
    private const int MaximumFileNumber = 511;

    private static readonly Dictionary<int, FileStream> OpenFiles = new();

    public static void OpenBinary(int fileNumber, string path)
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
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
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
