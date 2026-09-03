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
    private const int PrintZoneWidth = 14;
    private static IEnumerator<string>? _directoryEnumerator;

    /// <summary>Deletes one filesystem path using the VB6 Kill contract.</summary>
    public static void Kill(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        RequireExistingFile(path);
        File.Delete(path);
    }

    /// <summary>Copies one filesystem file without overwriting an existing destination.</summary>
    public static void FileCopy(string source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        File.Copy(source, destination, overwrite: false);
    }

    /// <summary>Renames one file or directory without overwriting an existing destination.</summary>
    public static void Rename(string source, string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
            return;
        }

        File.Move(source, destination, overwrite: false);
    }

    /// <summary>Creates one directory and reports an existing path as a VB6 filesystem error.</summary>
    public static void MakeDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Directory.Exists(path))
        {
            throw new IOException($"Directory '{path}' already exists.");
        }

        Directory.CreateDirectory(path);
    }

    /// <summary>Removes one empty directory, matching the VB6 RmDir contract.</summary>
    public static void RemoveDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.Delete(path, recursive: false);
    }

    /// <summary>Changes the process current directory used by relative VB6 paths.</summary>
    public static void ChangeDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.SetCurrentDirectory(path);
    }

    /// <summary>
    /// Returns the current directory. A drive argument is accepted for source compatibility; the
    /// portable managed profile can only provide the active directory for the current drive.
    /// </summary>
    public static string CurrentDirectory(string drive)
    {
        if (string.IsNullOrWhiteSpace(drive))
        {
            return Directory.GetCurrentDirectory();
        }

        var value = drive.Trim();
        if (value.Length != 1 || !char.IsLetter(value[0]))
        {
            throw new ArgumentException("CurDir expects a drive letter.", nameof(drive));
        }

        var current = Directory.GetCurrentDirectory();
        var root = Path.GetPathRoot(current);
        if (root is not null &&
            root.Length >= 1 &&
            char.ToUpperInvariant(root[0]) == char.ToUpperInvariant(value[0]))
        {
            return current;
        }

        if (OperatingSystem.IsWindows())
        {
            return value.ToUpperInvariant() + ":\\";
        }

        throw new DriveNotFoundException($"Drive '{value}' is not available on this host.");
    }

    /// <summary>Returns VB6 file and directory attribute bits for a filesystem path.</summary>
    public static int GetAttributes(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var attributes = File.GetAttributes(path);
        var result = 0;
        if (attributes.HasFlag(FileAttributes.ReadOnly)) result |= 1;
        if (attributes.HasFlag(FileAttributes.Hidden)) result |= 2;
        if (attributes.HasFlag(FileAttributes.System)) result |= 4;
        if (attributes.HasFlag(FileAttributes.Directory)) result |= 16;
        if (attributes.HasFlag(FileAttributes.Archive)) result |= 32;
        return result;
    }

    /// <summary>Applies the VB6 read-only/hidden/system/directory/archive attribute bits.</summary>
    public static void SetAttributes(string path, int attributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if ((attributes & ~63) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attributes),
                "VB6 file attributes must use the values 1, 2, 4, 16, or 32.");
        }

        var value = FileAttributes.Normal;
        if ((attributes & 1) != 0) value |= FileAttributes.ReadOnly;
        if ((attributes & 2) != 0) value |= FileAttributes.Hidden;
        if ((attributes & 4) != 0) value |= FileAttributes.System;
        if ((attributes & 32) != 0) value |= FileAttributes.Archive;
        File.SetAttributes(path, value);
    }

    /// <summary>Returns a file's last-write timestamp as a VB6/OLE Automation Date.</summary>
    public static double FileDateTime(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        RequireExistingFile(path);
        return File.GetLastWriteTime(path).ToOADate();
    }

    /// <summary>
    /// Rejects a missing path with the VB6 "File not found" error. Some framework calls are silent
    /// about a missing file - <see cref="File.Delete(string)"/> succeeds and
    /// <see cref="File.GetLastWriteTime(string)"/> answers with a 1601 placeholder - so the check
    /// has to happen before them. Throwing the framework exception keeps the error number defined
    /// in one place, in the mapping in <see cref="VBErrors"/>.
    /// </summary>
    private static void RequireExistingFile(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("File not found.", path);
        }
    }

    /// <summary>Implements Dir's stateful first-call/continuation form and attribute filtering.</summary>
    public static string Dir(string path, int attributes)
    {
        if (!string.IsNullOrEmpty(path))
        {
            var directory = Path.GetDirectoryName(path);
            var pattern = Path.GetFileName(path);
            directory = string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
            _directoryEnumerator?.Dispose();
            _directoryEnumerator = Directory.Exists(directory)
                ? Directory.EnumerateFileSystemEntries(directory, string.IsNullOrEmpty(pattern) ? "*" : pattern)
                    .Where(entry => MatchesDirAttributes(entry, attributes))
                    .GetEnumerator()
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

    private static bool MatchesDirAttributes(string path, int requested)
    {
        // Volume labels have no portable filesystem entry. Returning no match keeps vbVolume
        // explicit instead of silently returning an unrelated file.
        if ((requested & 8) != 0)
        {
            return false;
        }

        var actual = File.GetAttributes(path);
        var isDirectory = actual.HasFlag(FileAttributes.Directory);
        if (isDirectory && (requested & 16) == 0)
        {
            return false;
        }

        if (actual.HasFlag(FileAttributes.Hidden) && (requested & 2) == 0)
        {
            return false;
        }

        if (actual.HasFlag(FileAttributes.System) && (requested & 4) == 0)
        {
            return false;
        }

        return true;
    }

    public static long Length(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new FileInfo(path).Length;
    }

    private const int MinimumFileNumber = 1;
    private const int MaximumFileNumber = 511;

    private enum VBFileAccessMode
    {
        Binary,
        Input,
        Output,
        Append,
        Random
    }

    private enum VBFileSharingMode
    {
        Shared,
        LockRead,
        LockWrite,
        LockReadWrite
    }

    private enum VBFileOpenAccess
    {
        Default,
        Read,
        Write,
        ReadWrite
    }

    private static readonly Dictionary<int, FileStream> OpenFiles = new();
    private static readonly Dictionary<int, int?> RecordLengths = new();
    private static readonly Dictionary<int, long> RecordStarts = new();
    private static readonly Dictionary<int, VBFileAccessMode> AccessModes = new();
    private static readonly Dictionary<int, int> PrintWidths = new();
    private static readonly Dictionary<int, int> PrintLineLengths = new();
    private static readonly HashSet<int> WriteChannels = new();
    private readonly record struct FileLockRange(long Offset, long Length);
    private static readonly Dictionary<int, List<FileLockRange>> FileLocks = new();
    private static readonly Encoding FixedStringEncoding = Encoding.Latin1;

    private static Encoding TextEncoding(VBCompatibilityProfile compatibilityProfile) =>
        compatibilityProfile == VBCompatibilityProfile.VB6Sp6
            ? VBStrings.GetAnsiEncoding(compatibilityProfile)
            : Encoding.UTF8;

    public static void OpenBinary(int fileNumber, string path)
        => OpenBinary(fileNumber, path, (int)VBFileSharingMode.Shared);

    public static void OpenBinary(int fileNumber, string path, int sharingMode)
    {
        OpenBinary(fileNumber, path, (int)VBFileOpenAccess.Default, sharingMode);
    }

    public static void OpenBinary(int fileNumber, string path, int accessMode, int sharingMode)
    {
        OpenFile(fileNumber, path, FileMode.OpenOrCreate, ResolveFileAccess(accessMode, FileAccess.ReadWrite), recordLength: null, VBFileAccessMode.Binary, ToFileShare(sharingMode));
    }

    public static void OpenInput(int fileNumber, string path)
        => OpenInput(fileNumber, path, (int)VBFileSharingMode.Shared);

    public static void OpenInput(int fileNumber, string path, int sharingMode)
    {
        OpenInput(fileNumber, path, (int)VBFileOpenAccess.Default, sharingMode);
    }

    public static void OpenInput(int fileNumber, string path, int accessMode, int sharingMode)
    {
        var access = ResolveFileAccess(accessMode, FileAccess.Read);
        ValidateModeAccess(VBFileAccessMode.Input, access);
        OpenFile(fileNumber, path, FileMode.Open, access, recordLength: null, VBFileAccessMode.Input, ToFileShare(sharingMode));
    }

    public static void OpenOutput(int fileNumber, string path)
        => OpenOutput(fileNumber, path, (int)VBFileSharingMode.Shared);

    public static void OpenOutput(int fileNumber, string path, int sharingMode)
    {
        OpenOutput(fileNumber, path, (int)VBFileOpenAccess.Default, sharingMode);
    }

    public static void OpenOutput(int fileNumber, string path, int accessMode, int sharingMode)
    {
        var access = ResolveFileAccess(accessMode, FileAccess.Write);
        ValidateModeAccess(VBFileAccessMode.Output, access);
        OpenFile(fileNumber, path, FileMode.Create, access, recordLength: null, VBFileAccessMode.Output, ToFileShare(sharingMode));
    }

    public static void OpenAppend(int fileNumber, string path)
        => OpenAppend(fileNumber, path, (int)VBFileSharingMode.Shared);

    public static void OpenAppend(int fileNumber, string path, int sharingMode)
    {
        OpenAppend(fileNumber, path, (int)VBFileOpenAccess.Default, sharingMode);
    }

    public static void OpenAppend(int fileNumber, string path, int accessMode, int sharingMode)
    {
        var access = ResolveFileAccess(accessMode, FileAccess.Write);
        ValidateModeAccess(VBFileAccessMode.Append, access);
        var mode = access == FileAccess.Write ? FileMode.Append : FileMode.OpenOrCreate;
        OpenFile(fileNumber, path, mode, access, recordLength: null, VBFileAccessMode.Append, ToFileShare(sharingMode));
        if (access != FileAccess.Write)
        {
            GetStream(fileNumber).Position = GetStream(fileNumber).Length;
        }
    }

    public static void OpenRandom(int fileNumber, string path, int recordLength)
        => OpenRandom(fileNumber, path, recordLength, (int)VBFileSharingMode.Shared);

    public static void OpenRandom(int fileNumber, string path, int recordLength, int sharingMode)
    {
        OpenRandom(fileNumber, path, recordLength, (int)VBFileOpenAccess.Default, sharingMode);
    }

    public static void OpenRandom(int fileNumber, string path, int recordLength, int accessMode, int sharingMode)
    {
        if (recordLength is < 1 or > 32767)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordLength),
                "VB6 Random record lengths must be between 1 and 32767 bytes.");
        }

        OpenFile(fileNumber, path, FileMode.OpenOrCreate, ResolveFileAccess(accessMode, FileAccess.ReadWrite), recordLength, VBFileAccessMode.Random, ToFileShare(sharingMode));
    }

    /// <summary>Closes every open VB6 file channel, matching the <c>Reset</c> statement.</summary>
    public static void Reset() => CloseAll();

    /// <summary>
    /// Sets the output line width for a file. Width zero disables automatic wrapping; other values
    /// are the number of characters allowed before <c>Print #</c> starts a new line.
    /// </summary>
    public static void Width(int fileNumber, int width)
    {
        _ = GetStream(fileNumber);
        if (width is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "VB6 output widths must be between 0 and 255 characters.");
        }

        PrintWidths[fileNumber] = width;
        PrintLineLengths[fileNumber] = 0;
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
        => Print(fileNumber, value, VBCompatibilityProfile.Deterministic);

    /// <summary>Writes a single <c>Print #</c> value using the selected text-file encoding.</summary>
    public static void Print(int fileNumber, object? value, VBCompatibilityProfile compatibilityProfile)
        => PrintValue(fileNumber, value, endRecord: true, separator: 0, compatibilityProfile);

    /// <summary>
    /// Writes one value using the VB6 <c>Print #</c> output-list rules.
    /// Separator 0 is the first value, 1 is a semicolon (no padding), and 2
    /// is a comma (the next print zone). The caller controls whether the
    /// record is terminated so a trailing semicolon can continue on the next
    /// statement.
    /// </summary>
    public static void PrintValue(int fileNumber, object? value, bool endRecord, int separator)
        => PrintValue(fileNumber, value, endRecord, separator, VBCompatibilityProfile.Deterministic);

    /// <summary>
    /// Writes one <c>Print #</c> value using a profile-specific sequential text encoding. The
    /// deterministic profile deliberately retains UTF-8, whereas VB6Sp6 uses the active ANSI
    /// code page supplied by the Windows host.
    /// </summary>
    public static void PrintValue(
        int fileNumber,
        object? value,
        bool endRecord,
        int separator,
        VBCompatibilityProfile compatibilityProfile)
    {
        if (separator is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(separator), "VB6 Print # separators must be 0, 1 or 2.");
        }

        var stream = GetStream(fileNumber);
        var width = PrintWidths.GetValueOrDefault(fileNumber);
        var lineLength = PrintLineLengths.GetValueOrDefault(fileNumber);
        if (separator == 2)
        {
            var zoneOffset = lineLength % PrintZoneWidth;
            var spaces = PrintZoneWidth - zoneOffset;
            WritePrintText(stream, new string(' ', spaces), width, ref lineLength, TextEncoding(compatibilityProfile));
        }

        WritePrintText(
            stream,
            value is VBPrintPosition position ? VBDebug.ResolvePrintPosition(position, lineLength) : VBDebug.Format(value),
            width,
            ref lineLength,
            TextEncoding(compatibilityProfile));
        if (endRecord)
        {
            var terminator = TextEncoding(compatibilityProfile).GetBytes("\r\n");
            stream.Write(terminator, 0, terminator.Length);
            stream.Flush();
            lineLength = 0;
        }

        PrintLineLengths[fileNumber] = lineLength;
    }

    private static void WritePrintText(
        FileStream stream,
        string text,
        int width,
        ref int lineLength,
        Encoding encoding)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (width == 0)
        {
            var bytes = encoding.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            lineLength += text.Length;
            return;
        }

        var offset = 0;
        while (offset < text.Length)
        {
            if (lineLength >= width)
            {
                WritePrintNewLine(stream, encoding);
                lineLength = 0;
            }

            var count = Math.Min(width - lineLength, text.Length - offset);
            var bytes = encoding.GetBytes(text.Substring(offset, count));
            stream.Write(bytes, 0, bytes.Length);
            offset += count;
            lineLength += count;
            if (offset < text.Length)
            {
                WritePrintNewLine(stream, encoding);
                lineLength = 0;
            }
        }
    }

    private static void WritePrintNewLine(FileStream stream, Encoding encoding)
    {
        var terminator = encoding.GetBytes("\r\n");
        stream.Write(terminator, 0, terminator.Length);
    }

    /// <summary>Writes one value using VB6's machine-readable <c>Write #</c> representation.</summary>
    public static void Write(int fileNumber, object? value)
        => Write(fileNumber, value, VBCompatibilityProfile.Deterministic);

    /// <summary>Writes a single <c>Write #</c> value using the selected text-file encoding.</summary>
    public static void Write(int fileNumber, object? value, VBCompatibilityProfile compatibilityProfile)
        => WriteValue(fileNumber, value, endRecord: true, compatibilityProfile);

    /// <summary>Writes one value and optionally finishes the current <c>Write #</c> record.</summary>
    public static void WriteValue(int fileNumber, object? value, bool endRecord)
        => WriteValue(fileNumber, value, endRecord, VBCompatibilityProfile.Deterministic);

    /// <summary>Writes one <c>Write #</c> field with profile-specific sequential text encoding.</summary>
    public static void WriteValue(
        int fileNumber,
        object? value,
        bool endRecord,
        VBCompatibilityProfile compatibilityProfile)
    {
        var stream = GetStream(fileNumber);
        var encoding = TextEncoding(compatibilityProfile);
        if (!WriteChannels.Add(fileNumber))
        {
            var separator = encoding.GetBytes(",");
            stream.Write(separator, 0, separator.Length);
        }

        var bytes = encoding.GetBytes(FormatWriteValue(value));
        stream.Write(bytes, 0, bytes.Length);
        if (endRecord)
        {
            var terminator = encoding.GetBytes("\r\n");
            stream.Write(terminator, 0, terminator.Length);
            stream.Flush();
            WriteChannels.Remove(fileNumber);
        }
    }

    private static string FormatWriteValue(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfMissing(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsNull(value))
        {
            return "#NULL#";
        }

        if (VBVariants.IsEmpty(value))
        {
            return string.Empty;
        }

        return value switch
        {
            string text => $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"",
            bool boolean => boolean ? "#TRUE#" : "#FALSE#",
            VBDateValue date => $"#{DateTime.FromOADate(date.OADate).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}#",
            DateTime date => $"#{date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}#",
            VBErrorValue error => $"#ERROR {error.Code.ToString(CultureInfo.InvariantCulture)}#",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => VBConversions.CStr(value)
        };
    }

    public static string LineInput(int fileNumber)
        => LineInput(fileNumber, VBCompatibilityProfile.Deterministic);

    /// <summary>Reads one text line using the selected sequential file encoding.</summary>
    public static string LineInput(int fileNumber, VBCompatibilityProfile compatibilityProfile)
    {
        var stream = GetStream(fileNumber);
        SkipUtf8Bom(stream, compatibilityProfile);
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

        return TextEncoding(compatibilityProfile).GetString(bytes.ToArray());
    }

    public static string InputField(int fileNumber)
    {
        return InputField(fileNumber, VBCompatibilityProfile.Deterministic);
    }

    /// <summary>Reads one <c>Input #</c> field using the selected sequential file encoding.</summary>
    public static string InputField(int fileNumber, VBCompatibilityProfile compatibilityProfile) =>
        ReadInputField(fileNumber, compatibilityProfile);

    /// <summary>
    /// Reads one machine-readable <c>Input #</c> field.  Fields produced by <c>Write #</c>
    /// retain their Variant state instead of being reduced to text: quoted values are Strings,
    /// <c>#NULL#</c> is Null, Boolean markers remain Boolean, date markers become Date Variants,
    /// and numeric fields use the narrowest VB-compatible scalar representation available.
    /// Unrecognised fields remain Strings so ordinary text files keep the historical behavior.
    /// </summary>
    public static object? InputValue(int fileNumber)
        => InputValue(fileNumber, VBCompatibilityProfile.Deterministic);

    /// <summary>Reads one typed <c>Input #</c> field using the selected sequential file encoding.</summary>
    public static object? InputValue(int fileNumber, VBCompatibilityProfile compatibilityProfile)
    {
        var field = ReadInputField(fileNumber, compatibilityProfile);
        if (field.Length == 0)
        {
            return VBVariants.EmptyValue();
        }

        var token = field.Trim();

        if (token.Equals("#NULL#", StringComparison.OrdinalIgnoreCase))
        {
            return VBVariants.NullValue();
        }

        if (token.Equals("#TRUE#", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (token.Equals("#FALSE#", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (token.StartsWith("#", StringComparison.Ordinal) &&
            token.EndsWith("#", StringComparison.Ordinal) &&
            token.Length > 2)
        {
            var dateText = token[1..^1].Trim();
            if (dateText.StartsWith("ERROR ", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(dateText[6..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var taggedErrorCode))
            {
                return new VBErrorValue(taggedErrorCode);
            }

            if (double.TryParse(dateText, NumberStyles.Float, CultureInfo.InvariantCulture, out var oaDate))
            {
                return new VBDateValue(oaDate);
            }

            if (DateTime.TryParse(
                    dateText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsedDate))
            {
                return new VBDateValue(parsedDate.ToOADate());
            }
        }

        if (token.StartsWith("Error ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(token[6..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var errorCode))
        {
            return new VBErrorValue(errorCode);
        }

        if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            if (integer >= short.MinValue && integer <= short.MaxValue)
            {
                return (short)integer;
            }

            if (integer >= int.MinValue && integer <= int.MaxValue)
            {
                return (int)integer;
            }

            return integer;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        return field;
    }

    private static string ReadInputField(int fileNumber, VBCompatibilityProfile compatibilityProfile)
    {
        var stream = GetStream(fileNumber);
        SkipUtf8Bom(stream, compatibilityProfile);
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

        return TextEncoding(compatibilityProfile).GetString(bytes.ToArray());
    }

    /// <summary>Reads a requested number of text bytes from the current file position.</summary>
    public static string Input(long numberOfCharacters, int fileNumber)
        => Input(numberOfCharacters, fileNumber, VBCompatibilityProfile.Deterministic);

    /// <summary>Reads text bytes using the selected sequential file encoding.</summary>
    public static string Input(long numberOfCharacters, int fileNumber, VBCompatibilityProfile compatibilityProfile)
    {
        if (numberOfCharacters < 0 || numberOfCharacters > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfCharacters));
        }

        var stream = GetStream(fileNumber);
        SkipUtf8Bom(stream, compatibilityProfile);
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

        return TextEncoding(compatibilityProfile).GetString(bytes, 0, offset);
    }

    private static void ConsumeLineFeed(FileStream stream)
    {
        var next = stream.ReadByte();
        if (next >= 0 && next != '\n')
        {
            stream.Position--;
        }
    }

    private static void SkipUtf8Bom(FileStream stream, VBCompatibilityProfile compatibilityProfile)
    {
        if (compatibilityProfile != VBCompatibilityProfile.Deterministic || stream.Position != 0)
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
        int? recordLength,
        VBFileAccessMode accessMode,
        FileShare share)
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
            share));
        RecordLengths.Add(fileNumber, recordLength);
        AccessModes.Add(fileNumber, accessMode);
        PrintWidths.Add(fileNumber, 0);
        PrintLineLengths.Add(fileNumber, 0);
    }

    private static FileAccess ResolveFileAccess(int accessMode, FileAccess defaultAccess) => accessMode switch
    {
        (int)VBFileOpenAccess.Default => defaultAccess,
        (int)VBFileOpenAccess.Read => FileAccess.Read,
        (int)VBFileOpenAccess.Write => FileAccess.Write,
        (int)VBFileOpenAccess.ReadWrite => FileAccess.ReadWrite,
        _ => throw new ArgumentOutOfRangeException(nameof(accessMode), "Invalid VB6 file access mode.")
    };

    private static void ValidateModeAccess(VBFileAccessMode mode, FileAccess access)
    {
        if (mode == VBFileAccessMode.Input && access == FileAccess.Write)
        {
            throw new ArgumentException("Input files must permit reading.", nameof(access));
        }

        if ((mode is VBFileAccessMode.Output or VBFileAccessMode.Append) && access == FileAccess.Read)
        {
            throw new ArgumentException("Output and Append files must permit writing.", nameof(access));
        }
    }

    private static FileShare ToFileShare(int sharingMode) => sharingMode switch
    {
        (int)VBFileSharingMode.Shared => FileShare.ReadWrite,
        (int)VBFileSharingMode.LockRead => FileShare.Write,
        (int)VBFileSharingMode.LockWrite => FileShare.Read,
        (int)VBFileSharingMode.LockReadWrite => FileShare.None,
        _ => throw new ArgumentOutOfRangeException(nameof(sharingMode), "Invalid VB6 file sharing mode.")
    };

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
        AccessModes.Remove(fileNumber);
        PrintWidths.Remove(fileNumber);
        PrintLineLengths.Remove(fileNumber);
        WriteChannels.Remove(fileNumber);
        FileLocks.Remove(fileNumber);
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
        AccessModes.Clear();
        PrintWidths.Clear();
        PrintLineLengths.Clear();
        WriteChannels.Clear();
        FileLocks.Clear();
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

    /// <summary>
    /// Returns the VB6 <c>Loc</c> value for the current file position. Random files report the
    /// current record number; sequential files report the current 128-byte block; binary files
    /// report the byte position of the last operation.
    /// </summary>
    public static long Location(int fileNumber)
    {
        var stream = GetStream(fileNumber);
        return AccessModes[fileNumber] switch
        {
            VBFileAccessMode.Random when GetRecordLength(fileNumber) is int recordLength
                => stream.Position / recordLength,
            VBFileAccessMode.Input or VBFileAccessMode.Output or VBFileAccessMode.Append
                => stream.Position / 128,
            _ => stream.Position
        };
    }

    /// <summary>
    /// Locks a VB6 binary/random record range. Sequential channels always lock the complete file,
    /// as specified by the VB6 Lock statement. Positions are one-based; zero/zero means whole file.
    /// </summary>
    public static void Lock(int fileNumber, long start, long end)
        => ApplyFileLock(fileNumber, start, end, unlock: false);

    /// <summary>Releases a range previously acquired through <see cref="Lock"/>.</summary>
    public static void Unlock(int fileNumber, long start, long end)
        => ApplyFileLock(fileNumber, start, end, unlock: true);

    private static void ApplyFileLock(int fileNumber, long start, long end, bool unlock)
    {
        var stream = GetStream(fileNumber);
        var range = ResolveFileLockRange(fileNumber, stream, start, end, unlock);
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("VB6 file locking requires Windows or Linux file-lock support.");
        }

        if (unlock)
        {
#pragma warning disable CA1416 // guarded above; FileStream locks are supported on Windows/Linux.
            stream.Unlock(range.Offset, range.Length);
#pragma warning restore CA1416
            if (FileLocks.TryGetValue(fileNumber, out var ranges))
            {
                ranges.Remove(range);
                if (ranges.Count == 0)
                {
                    FileLocks.Remove(fileNumber);
                }
            }

            return;
        }

#pragma warning disable CA1416 // guarded above; FileStream locks are supported on Windows/Linux.
        stream.Lock(range.Offset, range.Length);
#pragma warning restore CA1416
        if (!FileLocks.TryGetValue(fileNumber, out var locks))
        {
            locks = new List<FileLockRange>();
            FileLocks.Add(fileNumber, locks);
        }

        locks.Add(range);
    }

    private static FileLockRange ResolveFileLockRange(
        int fileNumber,
        FileStream stream,
        long start,
        long end,
        bool unlock)
    {
        if (start < 0 || end < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 lock positions cannot be negative.");
        }

        var mode = AccessModes[fileNumber];
        if (mode is VBFileAccessMode.Input or VBFileAccessMode.Output or VBFileAccessMode.Append)
        {
            if (unlock && FileLocks.TryGetValue(fileNumber, out var sequentialLocks) && sequentialLocks.Count > 0)
            {
                return sequentialLocks[^1];
            }

            return new FileLockRange(0, Math.Max(stream.Length, 1));
        }

        if (start == 0 && end == 0)
        {
            if (unlock && FileLocks.TryGetValue(fileNumber, out var wholeLocks) && wholeLocks.Count > 0)
            {
                return wholeLocks[^1];
            }

            return new FileLockRange(0, Math.Max(stream.Length, 1));
        }

        if (start == 0)
        {
            start = 1;
        }

        if (end == 0)
        {
            end = start;
        }

        if (start < 1 || end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 lock ranges must be one-based and ordered.");
        }

        var unit = mode == VBFileAccessMode.Random
            ? GetRecordLength(fileNumber) ?? throw new InvalidOperationException("Random file has no record length.")
            : 1;
        var offset = checked((start - 1) * (long)unit);
        var length = checked((end - start + 1) * (long)unit);
        return new FileLockRange(offset, length);
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

    /// <summary>
    /// Reads a scalar Variant from a binary file.  VB6 stores the Variant type tag before the
    /// payload; preserving that tag is required for Empty/Null/Error/Date values and for fields
    /// whose target is itself a Variant.
    /// </summary>
    public static object? GetVariant(int fileNumber, long? position)
    {
        var stream = Seek(fileNumber, position);
        var recordStart = stream.Position;
        byte[] ReadField(int count)
        {
            if (GetRecordLength(fileNumber) is int recordLength &&
                checked(stream.Position - recordStart + count) > recordLength)
            {
                throw new VB6RuntimeErrorException(
                    59,
                    $"The Variant payload exceeds the Random record length of {recordLength.ToString(CultureInfo.InvariantCulture)} bytes.");
            }

            return ReadRaw(stream, count, fileNumber);
        }

        var variantType = BitConverter.ToUInt16(ReadField(sizeof(ushort)));
        var value = ReadVariantPayload(variantType, ReadField);
        AdvanceRandomRecord(fileNumber, stream, recordStart, forWrite: false);
        return value;
    }

    public static object? GetVariant(int fileNumber) => GetVariant(fileNumber, null);
    public static object? GetVariant(int fileNumber, long position) => GetVariant(fileNumber, (long?)position);

    /// <summary>Reads a Variant field while a UDT record walker owns the record boundary.</summary>
    public static object? GetRawVariant(int fileNumber)
    {
        var variantType = BitConverter.ToUInt16(ReadRecordRaw(fileNumber, sizeof(ushort)));
        return ReadVariantPayload(variantType, count => ReadRecordRaw(fileNumber, count));
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

    /// <summary>
    /// Reads a top-level dynamic-array descriptor only for Random files. Binary Get transfers an
    /// already allocated array's elements without a descriptor, while Random Get reconstructs the
    /// array shape written by Put.
    /// </summary>
    public static VBArray<T>? GetDynamicArrayIfRandom<T>(int fileNumber, VBArray<T>? existing)
        => GetRecordLength(fileNumber) is null ? existing : GetDynamicArray<T>(fileNumber);

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

    /// <summary>
    /// Writes a scalar Variant with its VB6/OLE Automation type tag and payload. Arrays and
    /// object references deliberately remain outside the binary record contract until their
    /// SAFEARRAY/COM ownership rules are available.
    /// </summary>
    public static void PutVariant(int fileNumber, long? position, object? value) =>
        Write(fileNumber, position, EncodeVariant(value));

    public static void PutVariant(int fileNumber, object? value) =>
        PutVariant(fileNumber, null, value);

    public static void PutVariant(int fileNumber, long position, object? value) =>
        PutVariant(fileNumber, (long?)position, value);

    /// <summary>Writes a Variant field while a UDT record walker owns the record boundary.</summary>
    public static void PutRawVariant(int fileNumber, object? value) =>
        WriteRecordRaw(fileNumber, EncodeVariant(value));

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

    /// <summary>Writes a top-level dynamic-array descriptor only for Random files.</summary>
    public static void PutDynamicArrayDescriptorIfRandom<T>(int fileNumber, VBArray<T>? value)
    {
        if (GetRecordLength(fileNumber) is not null)
        {
            PutDynamicArrayDescriptor(fileNumber, value);
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

    private static object? ReadVariantPayload(
        ushort variantType,
        Func<int, byte[]> readBytes)
    {
        if ((variantType & 0x2000) != 0)
        {
            throw new VB6TypeMismatchException("SAFEARRAY Variant records are not supported by the binary record contract yet.");
        }

        return variantType switch
        {
            0 => VBVariants.EmptyValue(),
            1 => VBVariants.NullValue(),
            2 => BitConverter.ToInt16(readBytes(sizeof(short))),
            3 => BitConverter.ToInt32(readBytes(sizeof(int))),
            4 => BitConverter.ToSingle(readBytes(sizeof(float))),
            5 => BitConverter.ToDouble(readBytes(sizeof(double))),
            6 => VBCurrency.FromScaled(BitConverter.ToInt64(readBytes(sizeof(long)))),
            7 => new VBDateValue(BitConverter.ToDouble(readBytes(sizeof(double)))),
            8 => ReadVariantString(readBytes),
            9 => VBVariants.NothingValue(),
            10 => new VBErrorValue(BitConverter.ToInt32(readBytes(sizeof(int)))),
            11 => BitConverter.ToInt16(readBytes(sizeof(short))) != 0,
            14 => ReadVariantDecimal(readBytes),
            17 => readBytes(sizeof(byte))[0],
            18 => BitConverter.ToUInt16(readBytes(sizeof(ushort))),
            19 => BitConverter.ToUInt32(readBytes(sizeof(uint))),
            20 => BitConverter.ToInt64(readBytes(sizeof(long))),
            21 => BitConverter.ToUInt64(readBytes(sizeof(ulong))),
            _ => throw new InvalidDataException(
                $"Unsupported binary Variant type tag {variantType.ToString(CultureInfo.InvariantCulture)}.")
        };
    }

    private static string ReadVariantString(Func<int, byte[]> readBytes)
    {
        var characterCount = BitConverter.ToUInt16(readBytes(sizeof(ushort)));
        if (characterCount == 0)
        {
            return string.Empty;
        }

        return Encoding.Unicode.GetString(readBytes(checked(characterCount * sizeof(char))));
    }

    private static decimal ReadVariantDecimal(Func<int, byte[]> readBytes)
    {
        var payload = readBytes(16);
        var scale = payload[2];
        var sign = payload[3] == 0x80 ? int.MinValue : 0;
        var hi = BitConverter.ToInt32(payload, 4);
        var lo = BitConverter.ToInt32(payload, 8);
        var mid = BitConverter.ToInt32(payload, 12);
        return new decimal(lo, mid, hi, sign != 0, scale);
    }

    private static byte[] EncodeVariant(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);
        VBVariants.ThrowIfMissing(value);

        var bytes = new List<byte>(16);
        void AddType(ushort type) => bytes.AddRange(BitConverter.GetBytes(type));
        void AddPayload(byte[] payload) => bytes.AddRange(payload);

        switch (value)
        {
            case null:
                AddType(0);
                break;
            case object when VBVariants.IsNull(value):
                AddType(1);
                break;
            case object when VBVariants.IsNothing(value):
                AddType(9);
                break;
            case VBErrorValue error:
                AddType(10);
                AddPayload(BitConverter.GetBytes(error.Code));
                break;
            case byte number:
                AddType(17);
                AddPayload(new[] { number });
                break;
            case short number:
                AddType(2);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case int number:
                AddType(3);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case long number:
                AddType(20);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case ushort number:
                AddType(18);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case uint number:
                AddType(19);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case ulong number:
                AddType(21);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case float number:
                AddType(4);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case double number:
                AddType(5);
                AddPayload(BitConverter.GetBytes(number));
                break;
            case VBCurrency currency:
                AddType(6);
                AddPayload(BitConverter.GetBytes(currency.ScaledValue));
                break;
            case VBDateValue date:
                AddType(7);
                AddPayload(BitConverter.GetBytes(date.OADate));
                break;
            case DateTime date:
                AddType(7);
                AddPayload(BitConverter.GetBytes(date.ToOADate()));
                break;
            case decimal number:
                AddType(14);
                AddPayload(EncodeVariantDecimal(number));
                break;
            case bool boolean:
                AddType(11);
                AddPayload(BitConverter.GetBytes(boolean ? (short)-1 : (short)0));
                break;
            case string text:
                if (text.Length > ushort.MaxValue)
                {
                    throw new OverflowException("VB6 Variant String transfers support at most 65535 characters.");
                }

                AddType(8);
                AddPayload(BitConverter.GetBytes((ushort)text.Length));
                AddPayload(Encoding.Unicode.GetBytes(text));
                break;
            case IntPtr pointer when IntPtr.Size == 8:
                AddType(20);
                AddPayload(BitConverter.GetBytes(pointer.ToInt64()));
                break;
            case IntPtr pointer:
                AddType(3);
                AddPayload(BitConverter.GetBytes(pointer.ToInt32()));
                break;
            default:
                throw new VB6TypeMismatchException(
                    $"Value of type '{value.GetType().Name}' cannot be stored in a binary Variant.");
        }

        return bytes.ToArray();
    }

    private static byte[] EncodeVariantDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        var payload = new byte[16];
        // DECIMAL's first two bytes are reserved; scale/sign occupy the next two bytes.
        payload[2] = (byte)((bits[3] >> 16) & 0x7F);
        payload[3] = (bits[3] & int.MinValue) != 0 ? (byte)0x80 : (byte)0;
        BitConverter.GetBytes(bits[2]).CopyTo(payload, 4);
        BitConverter.GetBytes(bits[0]).CopyTo(payload, 8);
        BitConverter.GetBytes(bits[1]).CopyTo(payload, 12);
        return payload;
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
            // 59 ist VB6s dokumentierte Nummer fuer eine Satzlaenge, die den Wert nicht fasst.
            // Eine generische Ausnahme traegt keine und fiel in VBErrors.Set in den Sammelwert 5.
            throw new VB6RuntimeErrorException(
                59,
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
            throw new VB6RuntimeErrorException(
                59,
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

    /// <summary>
    /// The mode an open channel was opened in. VB6 numbers them 1 Input, 2 Output, 4 Random,
    /// 8 Append and 32 Binary — the same bits the Open statement uses, so a program can compare
    /// against the vbFile* constants it already knows.
    ///
    /// A ReturnType of 2 asks for the DOS file handle. 32-bit VB6 has none to give and answers 5;
    /// the same is true here, and for the same reason: there is no such handle.
    /// </summary>
    public static int FileAttr(int fileNumber, int returnType)
    {
        ValidateFileNumber(fileNumber);
        if (returnType == 2)
        {
            VBErrors.Raise(
                5,
                "FileAttr",
                "FileAttr cannot report a DOS file handle; 32-bit VB6 has none either.",
                string.Empty,
                0);
        }

        if (returnType != 1)
        {
            VBErrors.Raise(5, "FileAttr", "Invalid procedure call or argument", string.Empty, 0);
        }

        if (!AccessModes.TryGetValue(fileNumber, out var mode))
        {
            throw new VB6RuntimeErrorException(
                52,
                $"VB6 file number {fileNumber.ToString(CultureInfo.InvariantCulture)} is not open.");
        }

        return mode switch
        {
            VBFileAccessMode.Input => 1,
            VBFileAccessMode.Output => 2,
            VBFileAccessMode.Random => 4,
            VBFileAccessMode.Append => 8,
            _ => 32
        };
    }

    private static FileStream GetStream(int fileNumber)
    {
        ValidateFileNumber(fileNumber);
        if (!OpenFiles.TryGetValue(fileNumber, out var stream))
        {
            // 52 is VB6's documented number for addressing a channel that is not open. A plain
            // InvalidOperationException carries none and fell into the catch-all 5, where it was
            // indistinguishable from every other unmapped failure.
            throw new VB6RuntimeErrorException(
                52,
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
