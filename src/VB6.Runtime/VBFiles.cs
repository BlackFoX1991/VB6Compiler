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
/// Fixed-size numeric types, variable-length binary Strings, basic text output, and scalar Random
/// records are supported.
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
    private static readonly Dictionary<int, int?> RecordLengths = new();
    private static readonly Dictionary<int, long> RecordStarts = new();
    private static readonly Encoding FixedStringEncoding = Encoding.Latin1;

    public static void OpenBinary(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.OpenOrCreate, FileAccess.ReadWrite, recordLength: null);
    }

    public static void OpenInput(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.Open, FileAccess.Read, recordLength: null);
    }

    public static void OpenOutput(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.Create, FileAccess.Write, recordLength: null);
    }

    public static void OpenAppend(int fileNumber, string path)
    {
        OpenFile(fileNumber, path, FileMode.Append, FileAccess.Write, recordLength: null);
    }

    public static void OpenRandom(int fileNumber, string path, int recordLength)
    {
        if (recordLength is < 1 or > 32767)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordLength),
                "VB6 Random record lengths must be between 1 and 32767 bytes.");
        }

        OpenFile(fileNumber, path, FileMode.OpenOrCreate, FileAccess.ReadWrite, recordLength);
    }

    public static void BeginRecord(int fileNumber, long? position)
    {
        var stream = GetStream(fileNumber);
        if (position is not null)
        {
            Seek(fileNumber, position.Value);
        }

        RecordStarts[fileNumber] = stream.Position;
    }

    public static void BeginRecord(int fileNumber) => BeginRecord(fileNumber, null);
    public static void BeginRecord(int fileNumber, long position) => BeginRecord(fileNumber, (long?)position);

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

    /// <summary>Reads a requested number of text bytes from the current file position.</summary>
    public static string Input(long numberOfCharacters, int fileNumber)
    {
        if (numberOfCharacters < 0 || numberOfCharacters > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfCharacters));
        }

        var stream = GetStream(fileNumber);
        SkipUtf8Bom(stream);
        var bytes = new byte[(int)numberOfCharacters];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return Encoding.UTF8.GetString(bytes, 0, offset);
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

    private static void OpenFile(
        int fileNumber,
        string path,
        FileMode mode,
        FileAccess access,
        int? recordLength)
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
        RecordLengths.Add(fileNumber, recordLength);
    }

    public static void Close(int fileNumber)
    {
        ValidateFileNumber(fileNumber);
        if (!OpenFiles.Remove(fileNumber, out var stream))
        {
            return;
        }

        stream.Dispose();
        RecordLengths.Remove(fileNumber);
        RecordStarts.Remove(fileNumber);
    }

    public static void CloseAll()
    {
        foreach (var stream in OpenFiles.Values)
        {
            stream.Dispose();
        }

        OpenFiles.Clear();
        RecordLengths.Clear();
        RecordStarts.Clear();
    }

    public static void EndRecord(int fileNumber, bool forWrite)
    {
        var stream = GetStream(fileNumber);
        if (RecordStarts.Remove(fileNumber, out var recordStart))
        {
            AdvanceRandomRecord(fileNumber, stream, recordStart, forWrite);
        }

        if (forWrite)
        {
            stream.Flush();
        }
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

        stream.Position = GetRecordLength(fileNumber) is int recordLength
            ? checked((position - 1) * recordLength)
            : position - 1;
    }

    public static long Position(int fileNumber)
    {
        var stream = GetStream(fileNumber);
        return GetRecordLength(fileNumber) is int recordLength
            ? checked(stream.Position / recordLength + 1)
            : stream.Position + 1;
    }

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
        var stream = Seek(fileNumber, position);
        var recordStart = stream.Position;
        EnsureRecordFits(fileNumber, sizeof(ushort));
        var characterCount = BitConverter.ToUInt16(ReadRaw(stream, 2));
        var byteCount = checked(characterCount * sizeof(char));
        EnsureRecordFits(fileNumber, byteCount + sizeof(ushort));
        if (characterCount == 0)
        {
            AdvanceRandomRecord(fileNumber, stream, recordStart, forWrite: false);
            return string.Empty;
        }

        var bytes = ReadRaw(stream, byteCount);
        AdvanceRandomRecord(fileNumber, stream, recordStart, forWrite: false);
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

    public static byte GetRawByte(int fileNumber) => ReadRecordRaw(fileNumber, 1)[0];
    public static short GetRawInteger(int fileNumber) => BitConverter.ToInt16(ReadRecordRaw(fileNumber, 2));
    public static int GetRawLong(int fileNumber) => BitConverter.ToInt32(ReadRecordRaw(fileNumber, 4));
    public static long GetRawLongLong(int fileNumber) => BitConverter.ToInt64(ReadRecordRaw(fileNumber, 8));
    public static float GetRawSingle(int fileNumber) => BitConverter.ToSingle(ReadRecordRaw(fileNumber, 4));
    public static double GetRawDouble(int fileNumber) => BitConverter.ToDouble(ReadRecordRaw(fileNumber, 8));
    public static VBCurrency GetRawCurrency(int fileNumber) =>
        VBCurrency.FromScaled(BitConverter.ToInt64(ReadRecordRaw(fileNumber, 8)));
    public static bool GetRawBoolean(int fileNumber) => BitConverter.ToInt16(ReadRecordRaw(fileNumber, 2)) != 0;

    public static string GetRawString(int fileNumber)
    {
        var characterCount = BitConverter.ToUInt16(ReadRecordRaw(fileNumber, 2));
        var bytes = ReadRecordRaw(fileNumber, checked(characterCount * sizeof(char)));
        return Encoding.Unicode.GetString(bytes);
    }

    /// <summary>
    /// Reads a fixed-length UDT String without the variable-string length descriptor. The current
    /// managed profile uses one Latin-1 byte per fixed-string character; host code-page selection
    /// remains a later compatibility boundary.
    /// </summary>
    public static string GetRawFixedString(int fileNumber, int length)
    {
        ValidateFixedStringLength(length);
        return FixedStringEncoding.GetString(ReadRecordRaw(fileNumber, length));
    }

    /// <summary>
    /// Reads the descriptor that VB6 stores before an array member of a user-defined type. The
    /// descriptor contains a two-byte rank followed by one 32-bit lower/upper bound pair per
    /// dimension. A zero rank represents an unallocated dynamic array.
    /// </summary>
    public static VBArray<T>? GetDynamicArray<T>(int fileNumber)
    {
        var rank = GetRawInteger(fileNumber);
        if (rank == 0)
        {
            return null;
        }

        if (rank is < 0 or > 60)
        {
            throw new InvalidDataException(
                $"The dynamic UDT array descriptor has an invalid rank of {rank.ToString(CultureInfo.InvariantCulture)}.");
        }

        var bounds = new VBArrayBound[rank];
        for (var dimension = 0; dimension < rank; dimension++)
        {
            bounds[dimension] = new VBArrayBound(
                GetRawLong(fileNumber),
                GetRawLong(fileNumber));
        }

        return new VBArray<T>(bounds);
    }

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

    public static void PutRaw(int fileNumber, byte value) => WriteRecordRaw(fileNumber, new[] { value });
    public static void PutRaw(int fileNumber, short value) => WriteRecordRaw(fileNumber, BitConverter.GetBytes(value));
    public static void PutRaw(int fileNumber, int value) => WriteRecordRaw(fileNumber, BitConverter.GetBytes(value));
    public static void PutRaw(int fileNumber, long value) => WriteRecordRaw(fileNumber, BitConverter.GetBytes(value));
    public static void PutRaw(int fileNumber, float value) => WriteRecordRaw(fileNumber, BitConverter.GetBytes(value));
    public static void PutRaw(int fileNumber, double value) => WriteRecordRaw(fileNumber, BitConverter.GetBytes(value));
    public static void PutRaw(int fileNumber, VBCurrency value) =>
        WriteRecordRaw(fileNumber, BitConverter.GetBytes(value.ScaledValue));
    public static void PutRaw(int fileNumber, bool value) =>
        WriteRecordRaw(fileNumber, BitConverter.GetBytes(value ? (short)-1 : (short)0));

    public static void PutRaw(int fileNumber, string value)
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
        WriteRecordRaw(fileNumber, bytes);
    }

    /// <summary>Writes a fixed-length UDT String as exactly its declared byte width.</summary>
    public static void PutRawFixedString(int fileNumber, string value, int length)
    {
        ValidateFixedStringLength(length);
        var fixedValue = VBTypeStorage.WriteFixedString(value, length);
        WriteRecordRaw(fileNumber, FixedStringEncoding.GetBytes(fixedValue));
    }

    /// <summary>
    /// Writes the descriptor that VB6 prefixes to an array member of a user-defined type. The
    /// element payload is emitted separately so nested UDT members can use the same record walker.
    /// </summary>
    public static void PutDynamicArrayDescriptor<T>(int fileNumber, VBArray<T>? value)
    {
        var rank = value?.Rank ?? 0;
        if (rank > 60)
        {
            throw new InvalidDataException("Dynamic UDT arrays support at most 60 dimensions.");
        }

        PutRaw(fileNumber, checked((short)rank));
        if (value is null)
        {
            return;
        }

        for (var dimension = 1; dimension <= rank; dimension++)
        {
            PutRaw(fileNumber, value.LBound(dimension));
            PutRaw(fileNumber, value.UBound(dimension));
        }
    }

    private static byte[] Read(int fileNumber, long? position, int count)
    {
        var stream = Seek(fileNumber, position);
        EnsureRecordFits(fileNumber, count);
        var recordStart = stream.Position;
        var buffer = ReadRaw(stream, count, fileNumber);
        AdvanceRandomRecord(fileNumber, stream, recordStart, forWrite: false);
        return buffer;
    }

    private static byte[] ReadRecordRaw(int fileNumber, int count)
    {
        var stream = GetStream(fileNumber);
        EnsureRecordFieldFits(fileNumber, stream.Position, count);
        return ReadRaw(stream, count, fileNumber);
    }

    private static void Write(int fileNumber, long? position, byte[] bytes)
    {
        var stream = Seek(fileNumber, position);
        EnsureRecordFits(fileNumber, bytes.Length);
        var recordStart = stream.Position;
        stream.Write(bytes, 0, bytes.Length);
        AdvanceRandomRecord(fileNumber, stream, recordStart, forWrite: true);
        stream.Flush();
    }

    private static void WriteRecordRaw(int fileNumber, byte[] bytes)
    {
        var stream = GetStream(fileNumber);
        EnsureRecordFieldFits(fileNumber, stream.Position, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static byte[] ReadRaw(FileStream stream, int count, int? fileNumber = null)
    {
        var buffer = new byte[count];
        var read = stream.ReadAtLeast(buffer, count, throwOnEndOfStream: false);
        if (read < count)
        {
            var prefix = fileNumber is null
                ? "The file"
                : $"VB6 file number {fileNumber.Value.ToString(CultureInfo.InvariantCulture)}";
            throw new EndOfStreamException(
                $"{prefix} has fewer than {count.ToString(CultureInfo.InvariantCulture)} bytes left to read.");
        }

        return buffer;
    }

    private static void ValidateFixedStringLength(int length)
    {
        if (length is < 1 or > 65526)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "VB6 fixed-length String fields must contain between 1 and 65526 characters.");
        }
    }

    private static void EnsureRecordFits(int fileNumber, int count)
    {
        if (GetRecordLength(fileNumber) is int recordLength && count > recordLength)
        {
            throw new InvalidOperationException(
                $"The value requires {count.ToString(CultureInfo.InvariantCulture)} bytes, but the " +
                $"Random record length is only {recordLength.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void EnsureRecordFieldFits(int fileNumber, long position, int count)
    {
        if (GetRecordLength(fileNumber) is not int recordLength ||
            !RecordStarts.TryGetValue(fileNumber, out var recordStart))
        {
            return;
        }

        var consumed = checked(position - recordStart + count);
        if (consumed > recordLength)
        {
            throw new InvalidOperationException(
                $"The record requires {consumed.ToString(CultureInfo.InvariantCulture)} bytes, but the " +
                $"Random record length is only {recordLength.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void AdvanceRandomRecord(
        int fileNumber,
        FileStream stream,
        long recordStart,
        bool forWrite)
    {
        if (GetRecordLength(fileNumber) is not int recordLength)
        {
            return;
        }

        var recordEnd = checked(recordStart + recordLength);
        if (forWrite && stream.Length < recordEnd)
        {
            stream.SetLength(recordEnd);
        }

        stream.Position = recordEnd;
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

    private static int? GetRecordLength(int fileNumber) =>
        RecordLengths.TryGetValue(fileNumber, out var recordLength) ? recordLength : null;

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
