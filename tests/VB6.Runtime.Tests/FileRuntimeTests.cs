using System.Text;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class FileRuntimeTests
{
    [TestMethod]
    public void PutAndGet_RoundTripFixedSizeNumericTypes()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenBinary(1, path);
            VBFiles.Put(1, 1, (byte)7);
            VBFiles.Put(1, null, (short)-300);
            VBFiles.Put(1, null, 70000);
            VBFiles.Put(1, null, 1.5d);
            VBFiles.Put(1, null, VBConversions.CCur(2.25m));

            Assert.AreEqual((byte)7, VBFiles.GetByte(1, 1));
            Assert.AreEqual((short)-300, VBFiles.GetInteger(1, null));
            Assert.AreEqual(70000, VBFiles.GetLong(1, null));
            Assert.AreEqual(1.5d, VBFiles.GetDouble(1, null));
            Assert.AreEqual(VBConversions.CCur(2.25m), VBFiles.GetCurrency(1, null));
            VBFiles.Close(1);
        });
    }

    /// <summary>VB6 file positions are one-based, so position 1 is the first byte.</summary>
    [TestMethod]
    public void Seek_UsesOneBasedPositions()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenBinary(1, path);
            VBFiles.Put(1, 1, (byte)10);
            VBFiles.Put(1, 2, (byte)20);
            VBFiles.Put(1, 3, (byte)30);

            VBFiles.Seek(1, 2);
            Assert.AreEqual(2L, VBFiles.Position(1));
            Assert.AreEqual((byte)20, VBFiles.GetByte(1, null));
            Assert.AreEqual((byte)30, VBFiles.GetByte(1, null), "Omitting the position continues reading.");
            Assert.AreEqual(3L, VBFiles.Length(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void Get_UsesTheExactVb6StorageSizes()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenBinary(1, path);
            VBFiles.Put(1, 1, (short)1);
            Assert.AreEqual(3L, VBFiles.Position(1), "An Integer occupies two bytes.");

            VBFiles.Put(1, null, 1);
            Assert.AreEqual(7L, VBFiles.Position(1), "A Long occupies four bytes.");

            VBFiles.Put(1, null, VBConversions.CCur(1m));
            Assert.AreEqual(15L, VBFiles.Position(1), "Currency occupies eight bytes.");
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void RandomRecords_UseFixedBoundariesAndOneBasedRecordPositions()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenRandom(1, path, 8);
            VBFiles.Put(1, 1, 10);
            Assert.AreEqual(2L, VBFiles.Position(1));
            VBFiles.Put(1, null, 20);
            Assert.AreEqual(3L, VBFiles.Position(1));
            Assert.AreEqual(16L, VBFiles.Length(1));
            VBFiles.Close(1);

            VBFiles.OpenRandom(1, path, 8);
            Assert.AreEqual(10, VBFiles.GetLong(1, 1));
            Assert.AreEqual(20, VBFiles.GetLong(1, null));
            Assert.AreEqual(3L, VBFiles.Position(1));
            Assert.ThrowsException<InvalidOperationException>(() => VBFiles.Put(1, 3, "too long"));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void Location_UsesTheVb6ModeSpecificUnits()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenBinary(1, path);
            Assert.AreEqual(0L, VBFiles.Location(1));
            VBFiles.Put(1, 1, (byte)10);
            Assert.AreEqual(1L, VBFiles.Location(1));
            VBFiles.Put(1, null, (byte)20);
            Assert.AreEqual(2L, VBFiles.Location(1));
            VBFiles.Close(1);

            VBFiles.OpenRandom(1, path, 4);
            Assert.AreEqual(0L, VBFiles.Location(1));
            VBFiles.Put(1, 1, 10);
            Assert.AreEqual(1L, VBFiles.Location(1));
            VBFiles.Close(1);

            File.WriteAllBytes(path, new byte[128]);
            VBFiles.OpenInput(1, path);
            Assert.AreEqual(0L, VBFiles.Location(1));
            _ = VBFiles.Input(128, 1);
            Assert.AreEqual(1L, VBFiles.Location(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void Reset_ClosesAllOpenFileChannels()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenOutput(1, path);
            VBFiles.OpenOutput(2, path);
            Assert.AreEqual(3, VBFiles.FreeFile());

            VBFiles.Reset();

            Assert.AreEqual(1, VBFiles.FreeFile());
            VBFiles.OpenInput(1, path);
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void Write_UsesMachineReadableScalarFormatting()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenOutput(1, path);
            VBFiles.Write(1, "a\"b");
            VBFiles.Write(1, true);
            VBFiles.Write(1, VBVariants.NullValue());
            VBFiles.Close(1);

            Assert.AreEqual("\"a\"\"b\"\r\n#TRUE#\r\n#NULL#\r\n", File.ReadAllText(path));
        });
    }

    [TestMethod]
    public void SequentialTextTransfers_RespectTheSelectedCompatibilityProfileCodePage()
    {
        WithTemporaryFile(path =>
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var ansi = Encoding.GetEncoding(
                0,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);

            VBFiles.OpenOutput(1, path);
            VBFiles.Print(1, "ä", VBCompatibilityProfile.VB6Sp6);
            VBFiles.Write(1, "ö", VBCompatibilityProfile.VB6Sp6);
            VBFiles.Close(1);

            CollectionAssert.AreEqual(
                ansi.GetBytes("ä\r\n\"ö\"\r\n"),
                File.ReadAllBytes(path),
                "VB6Sp6 sequential transfers use the active ANSI code page.");

            VBFiles.OpenInput(1, path);
            Assert.AreEqual("ä", VBFiles.LineInput(1, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("ö", VBFiles.InputField(1, VBCompatibilityProfile.VB6Sp6));
            VBFiles.Close(1);

            VBFiles.OpenOutput(1, path);
            VBFiles.Print(1, "ä");
            VBFiles.Close(1);

            CollectionAssert.AreEqual(
                Encoding.UTF8.GetBytes("ä\r\n"),
                File.ReadAllBytes(path),
                "The default overload remains deterministic UTF-8.");

            VBFiles.OpenInput(1, path);
            Assert.AreEqual("ä", VBFiles.LineInput(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void InputValue_RestoresWriteStateMarkersAndNumericValues()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenOutput(1, path);
            VBFiles.Write(1, "hello");
            VBFiles.Write(1, 42);
            VBFiles.Write(1, 1.25d);
            VBFiles.Write(1, true);
            VBFiles.Write(1, VBVariants.NullValue());
            VBFiles.Write(1, new VBDateValue(new DateTime(2020, 1, 2, 3, 4, 5).ToOADate()));
            VBFiles.Write(1, new VBErrorValue(32767));
            VBFiles.Close(1);

            VBFiles.OpenInput(1, path);
            Assert.AreEqual("hello", VBFiles.InputValue(1));
            Assert.AreEqual((short)42, VBFiles.InputValue(1));
            Assert.AreEqual(1.25d, VBFiles.InputValue(1));
            Assert.AreEqual(true, VBFiles.InputValue(1));
            Assert.IsTrue(VBVariants.IsNull(VBFiles.InputValue(1)));
            Assert.AreEqual(new VBDateValue(new DateTime(2020, 1, 2, 3, 4, 5).ToOADate()), VBFiles.InputValue(1));
            Assert.AreEqual(new VBErrorValue(32767), VBFiles.InputValue(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void BinaryVariantTransfers_PreserveScalarSubtypeAndPayload()
    {
        WithTemporaryFile(path =>
        {
            var date = new VBDateValue(new DateTime(2020, 1, 2, 3, 4, 5).ToOADate());
            var values = new object?[]
            {
                VBVariants.EmptyValue(),
                VBVariants.NullValue(),
                (short)-12,
                42,
                1.25d,
                VBConversions.CCur(2.5m),
                date,
                "hello",
                false,
                new VBErrorValue(32767),
                VBConversions.CDec("123.45")
            };

            VBFiles.OpenBinary(1, path);
            foreach (var value in values)
            {
                VBFiles.PutVariant(1, null, value);
            }

            VBFiles.Close(1);
            VBFiles.OpenBinary(1, path);
            foreach (var expected in values)
            {
                var actual = VBFiles.GetVariant(1, null);
                if (expected is null || VBVariants.IsNull(expected))
                {
                    Assert.AreEqual(VBVariants.VarType(expected), VBVariants.VarType(actual));
                }
                else
                {
                    Assert.AreEqual(expected, actual);
                }
            }

            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void Width_WrapsPrintContinuationAndValidatesRange()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenOutput(1, path);
            VBFiles.Width(1, 5);
            for (var value = 0; value < 10; value++)
            {
                VBFiles.PrintValue(1, value.ToString(), endRecord: false, separator: 0);
            }

            VBFiles.Close(1);

            Assert.AreEqual("01234\r\n56789", File.ReadAllText(path));
            VBFiles.OpenOutput(1, path);
            VBFiles.PrintValue(1, "a", endRecord: false, separator: 0);
            VBFiles.PrintValue(1, "b", endRecord: true, separator: 2);
            VBFiles.Close(1);
            Assert.AreEqual("a             b\r\n", File.ReadAllText(path));

            VBFiles.OpenOutput(1, path);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBFiles.Width(1, 256));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void LockAndUnlock_ApplyOneBasedBinaryAndRandomRanges()
    {
        WithTemporaryFile(path =>
        {
            File.WriteAllBytes(path, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());

            VBFiles.OpenBinary(1, path);
            VBFiles.Lock(1, 2, 4);
            VBFiles.Unlock(1, 2, 4);
            VBFiles.Lock(1, 0, 0);
            VBFiles.Unlock(1, 0, 0);
            VBFiles.Close(1);

            VBFiles.OpenRandom(1, path, 4);
            VBFiles.Lock(1, 2, 3);
            VBFiles.Unlock(1, 2, 3);
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void Open_ValidatesAndAppliesSharingModes()
    {
        WithTemporaryFile(path =>
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBFiles.OpenBinary(1, path, 99));

            for (var sharingMode = 0; sharingMode <= 3; sharingMode++)
            {
                VBFiles.OpenBinary(1, path, sharingMode);
                VBFiles.Close(1);
            }
        });
    }

    [TestMethod]
    public void Open_AccessClauseRestrictsReadAndWriteOperations()
    {
        WithTemporaryFile(path =>
        {
            File.WriteAllBytes(path, [10]);

            // Access Read permits reads but rejects writes at the managed file boundary.
            VBFiles.OpenBinary(1, path, 1, 0);
            Assert.AreEqual((byte)10, VBFiles.GetByte(1, 1));
            Assert.ThrowsException<NotSupportedException>(() => VBFiles.Put(1, 1, (byte)11));
            VBFiles.Close(1);

            // Access Write is the inverse contract.
            VBFiles.OpenBinary(1, path, 2, 0);
            Assert.ThrowsException<NotSupportedException>(() => VBFiles.GetByte(1, 1));
            VBFiles.Put(1, 1, (byte)12);
            VBFiles.Close(1);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBFiles.OpenBinary(1, path, 99, 0));
        });
    }

    [TestMethod]
    public void FixedStringRawTransfersUseDeclaredByteWidth()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenRandom(1, path, 5);
            VBFiles.BeginRecord(1, 1);
            VBFiles.PutRawFixedString(1, "Hi", 5);
            VBFiles.EndRecord(1, forWrite: true);
            VBFiles.Close(1);

            CollectionAssert.AreEqual(new byte[] { 72, 105, 32, 32, 32 }, File.ReadAllBytes(path));

            VBFiles.OpenRandom(1, path, 5);
            VBFiles.BeginRecord(1, 1);
            Assert.AreEqual("Hi   ", VBFiles.GetRawFixedString(1, 5));
            VBFiles.EndRecord(1, forWrite: false);
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void DynamicUdtArrayDescriptors_RoundTripRankBoundsAndPayloadShape()
    {
        WithTemporaryFile(path =>
        {
            var written = new VBArray<int>(
                new VBArrayBound(1, 2),
                new VBArrayBound(-1, 0));
            written[1, -1] = 10;
            written[2, 0] = 40;

            VBFiles.OpenBinary(1, path);
            VBFiles.PutDynamicArrayDescriptor(1, written);
            foreach (var value in written.EnumerateValues())
            {
                VBFiles.PutRaw(1, value);
            }
            VBFiles.Close(1);

            VBFiles.OpenBinary(1, path);
            var readBack = VBFiles.GetDynamicArray<int>(1);
            Assert.IsNotNull(readBack);
            Assert.AreEqual(2, readBack.Rank);
            Assert.AreEqual(1, readBack.LBound(1));
            Assert.AreEqual(2, readBack.UBound(1));
            Assert.AreEqual(-1, readBack.LBound(2));
            Assert.AreEqual(0, readBack.UBound(2));
            Assert.AreEqual(10, VBFiles.GetRawLong(1));
            Assert.AreEqual(0, VBFiles.GetRawLong(1));
            Assert.AreEqual(0, VBFiles.GetRawLong(1));
            Assert.AreEqual(40, VBFiles.GetRawLong(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void TextModes_CreateTruncateAppendAndWriteVb6Lines()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenOutput(1, path);
            VBFiles.Print(1, "hello");
            VBFiles.Print(1, 42);
            VBFiles.Close(1);

            Assert.AreEqual("hello\r\n 42\r\n", File.ReadAllText(path, System.Text.Encoding.UTF8));

            VBFiles.OpenAppend(1, path);
            VBFiles.Print(1, "tail");
            VBFiles.Close(1);
            Assert.AreEqual("hello\r\n 42\r\ntail\r\n", File.ReadAllText(path, System.Text.Encoding.UTF8));

            VBFiles.OpenInput(1, path);
            Assert.AreEqual(18L, VBFiles.Length(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void LineInput_ReadsUtf8LinesAndAdvancesTheFilePosition()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenOutput(1, path);
            VBFiles.Print(1, "Grüße");
            VBFiles.Print(1, "zweite");
            VBFiles.Close(1);

            VBFiles.OpenInput(1, path);
            Assert.AreEqual("Grüße", VBFiles.LineInput(1));
            Assert.AreEqual("zweite", VBFiles.LineInput(1));
            Assert.AreEqual(18L, VBFiles.Position(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void InputField_ReadsDelimitedAndQuotedStringFields()
    {
        WithTemporaryFile(path =>
        {
            File.WriteAllText(path, "one,\"two,three\",four\r\n", System.Text.Encoding.UTF8);
            VBFiles.OpenInput(1, path);

            Assert.AreEqual("one", VBFiles.InputField(1));
            Assert.AreEqual("two,three", VBFiles.InputField(1));
            Assert.AreEqual("four", VBFiles.InputField(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void Input_ReadsRequestedTextBytesAndAdvancesThePosition()
    {
        WithTemporaryFile(path =>
        {
            File.WriteAllText(path, "abcdef", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            VBFiles.OpenInput(1, path);

            Assert.AreEqual("abc", VBFiles.Input(3, 1));
            Assert.AreEqual("de", VBFiles.Input(2, 1));
            Assert.AreEqual(6L, VBFiles.Position(1));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void PutAndGet_RoundTripVariableLengthStringAndContinueFromPrefixPayload()
    {
        WithTemporaryFile(path =>
        {
            var value = "Gr" + (char)252 + (char)223 + "e";

            VBFiles.OpenBinary(1, path);
            VBFiles.Put(1, 1, value);
            VBFiles.Close(1);

            VBFiles.OpenBinary(1, path);
            Assert.AreEqual(value, VBFiles.GetString(1, 1));
            Assert.AreEqual(1L + sizeof(ushort) + value.Length * sizeof(char), VBFiles.Position(1));

            VBFiles.Seek(1, 1);
            Assert.AreEqual(value, VBFiles.GetString(1, null));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void PutString_RejectsValuesThatDoNotFitTheVb6LengthPrefix()
    {
        WithTemporaryFile(path =>
        {
            VBFiles.OpenBinary(1, path);
            Assert.ThrowsException<OverflowException>(() =>
                VBFiles.Put(1, 1, new string('x', ushort.MaxValue + 1)));
            VBFiles.Close(1);
        });
    }

    [TestMethod]
    public void FreeFile_ReturnsAnUnusedNumberAndCloseReleasesIt()
    {
        WithTemporaryFile(path =>
        {
            var first = VBFiles.FreeFile();
            VBFiles.OpenBinary(first, path);
            Assert.AreNotEqual(first, VBFiles.FreeFile());

            VBFiles.Close(first);
            Assert.AreEqual(first, VBFiles.FreeFile());
        });
    }

    [TestMethod]
    public void Operations_OnAClosedFileNumberFail()
    {
        VBFiles.CloseAll();
        Assert.ThrowsException<InvalidOperationException>(() => VBFiles.GetByte(1, null));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBFiles.GetByte(0, null));
    }

    [TestMethod]
    public void FilesystemPathOperations_CopyDirectoriesAndAttributes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerFilePathTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "source.txt");
        var copy = Path.Combine(directory, "copy.txt");
        var renamed = Path.Combine(directory, "renamed.txt");
        var nested = Path.Combine(directory, "nested");
        var renamedNested = Path.Combine(directory, "renamed-nested");

        try
        {
            File.WriteAllText(source, "hello");
            VBFiles.FileCopy(source, copy);
            Assert.AreEqual(5L, VBFiles.Length(copy));
            Assert.IsTrue(VBFiles.FileDateTime(copy) > 0d);
            VBFiles.Rename(copy, renamed);
            Assert.IsFalse(File.Exists(copy));
            Assert.AreEqual(5L, VBFiles.Length(renamed));

            VBFiles.MakeDirectory(nested);
            Assert.AreEqual(16, VBFiles.GetAttributes(nested) & 16);
            VBFiles.Rename(nested, renamedNested);
            Assert.IsFalse(Directory.Exists(nested));
            Assert.AreEqual(16, VBFiles.GetAttributes(renamedNested) & 16);
            VBFiles.SetAttributes(renamed, 1);
            Assert.AreEqual(1, VBFiles.GetAttributes(renamed) & 1);
            VBFiles.SetAttributes(renamed, 0);
            VBFiles.RemoveDirectory(renamedNested);
            Assert.ThrowsException<IOException>(() => VBFiles.MakeDirectory(directory));
        }
        finally
        {
            if (File.Exists(source)) File.Delete(source);
            if (File.Exists(copy)) File.Delete(copy);
            if (File.Exists(renamed)) File.Delete(renamed);
            if (Directory.Exists(nested)) Directory.Delete(nested);
            if (Directory.Exists(renamedNested)) Directory.Delete(renamedNested);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [TestMethod]
    public void Dir_ContinuesAndIncludesDirectoriesWhenRequested()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerDirTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(directory, "source.txt");
        var nested = Path.Combine(directory, "nested");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(source, "hello");
            Directory.CreateDirectory(nested);

            Assert.AreEqual("source.txt", VBFiles.Dir(Path.Combine(directory, "*"), 0));
            Assert.AreEqual(string.Empty, VBFiles.Dir(string.Empty, 0));

            var first = VBFiles.Dir(Path.Combine(directory, "*"), 16);
            var second = VBFiles.Dir(string.Empty, 16);
            Assert.IsTrue(new[] { first, second }.Contains("source.txt"));
            Assert.IsTrue(new[] { first, second }.Contains("nested"));
            Assert.AreEqual(string.Empty, VBFiles.Dir(string.Empty, 16));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void WithTemporaryFile(Action<string> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vb6files_{Guid.NewGuid():N}.bin");
        try
        {
            body(path);
        }
        finally
        {
            VBFiles.CloseAll();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
