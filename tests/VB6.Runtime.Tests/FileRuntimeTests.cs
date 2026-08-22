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
