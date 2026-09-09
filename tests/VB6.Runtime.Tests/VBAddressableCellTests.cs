using System.Runtime.InteropServices;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBAddressableCellTests
{
    [TestMethod]
    public void NativeCell_SynchronizesNativeAndManagedWritesAcrossCollection()
    {
        using var cell = VBAddressableCell<int>.Create(7);
        var address = cell.GetNativeAddress();

        Marshal.WriteInt32(address, 41);
        ForceFullCollection();

        Assert.AreEqual(address, cell.GetNativeAddress());
        Assert.AreEqual(41, cell.Read());

        cell.Write(42);
        Assert.AreEqual(42, Marshal.ReadInt32(address));
    }

    [TestMethod]
    public void NativeCell_RejectsAccessAfterDeterministicRelease()
    {
        var cell = VBAddressableCell<long>.Create(7);
        cell.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => _ = cell.GetNativeAddress());
        Assert.ThrowsException<ObjectDisposedException>(() => _ = cell.Read());
        Assert.ThrowsException<ObjectDisposedException>(() => cell.Write(8));
    }

    [TestMethod]
    public void Int32Facade_PreservesTheNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateInt32(7);
        var address = VBAddressableStorage.GetInt32NativeAddress(storage);

        Marshal.WriteInt32(address, 41);
        ForceFullCollection();

        Assert.AreEqual(41, VBAddressableStorage.ReadInt32(storage));
        VBAddressableStorage.WriteInt32(storage, 42);
        Assert.AreEqual(42, Marshal.ReadInt32(address));

        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadInt32(storage));
    }

    [TestMethod]
    public void UInt32Facade_PreservesTheFourByteUnsignedNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateUInt32(4_000_000_000u);
        var address = VBAddressableStorage.GetUInt32NativeAddress(storage);

        Assert.AreEqual(4_000_000_000u, unchecked((uint)Marshal.ReadInt32(address)));
        Marshal.WriteInt32(address, 123);
        ForceFullCollection();

        Assert.AreEqual(123u, VBAddressableStorage.ReadUInt32(storage));
        VBAddressableStorage.WriteUInt32(storage, 3_000_000_000u);
        Assert.AreEqual(3_000_000_000u, unchecked((uint)Marshal.ReadInt32(address)));

        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadUInt32(storage));
    }

    [TestMethod]
    public void Int16Facade_PreservesTheTwoByteNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateInt16(7);
        var address = VBAddressableStorage.GetInt16NativeAddress(storage);

        Marshal.WriteInt16(address, 41);
        ForceFullCollection();

        Assert.AreEqual((short)41, VBAddressableStorage.ReadInt16(storage));
        VBAddressableStorage.WriteInt16(storage, 42);
        Assert.AreEqual((short)42, Marshal.ReadInt16(address));

        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadInt16(storage));
    }

    [TestMethod]
    public void UShortFacade_PreservesTheTwoByteUnsignedNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateUShort(50_000);
        var address = VBAddressableStorage.GetUShortNativeAddress(storage);

        Assert.AreEqual(50_000, unchecked((ushort)Marshal.ReadInt16(address)));
        Marshal.WriteInt16(address, 123);
        ForceFullCollection();

        Assert.AreEqual((ushort)123, VBAddressableStorage.ReadUShort(storage));
        VBAddressableStorage.WriteUShort(storage, 40_000);
        Assert.AreEqual(40_000, unchecked((ushort)Marshal.ReadInt16(address)));

        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadUShort(storage));
    }

    [TestMethod]
    public void ByteFacade_PreservesTheSingleByteNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateByte(7);
        var address = VBAddressableStorage.GetByteNativeAddress(storage);

        Marshal.WriteByte(address, 41);
        ForceFullCollection();

        Assert.AreEqual((byte)41, VBAddressableStorage.ReadByte(storage));
        VBAddressableStorage.WriteByte(storage, 42);
        Assert.AreEqual((byte)42, Marshal.ReadByte(address));

        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadByte(storage));
    }

    [TestMethod]
    public void StringFacade_OwnsAGcStableBstrUntilReplacementOrRelease()
    {
        var storage = VBAddressableStorage.CreateString("abc");
        var address = VBAddressableStorage.GetStringNativeAddress(storage);

        Marshal.WriteInt16(address, 'Z');
        ForceFullCollection();
        Assert.AreEqual("Zbc", VBAddressableStorage.ReadString(storage));

        VBAddressableStorage.WriteString(storage, "xy");
        Assert.AreEqual("xy", VBAddressableStorage.ReadString(storage));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadString(storage));
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.WriteString(storage, "z"));
    }

    [TestMethod]
    public void StringDescriptorFacade_ReadsAndReplacesTheBstrOwnedByTheAddressableCell()
    {
        var storage = VBAddressableStorage.CreateString("abc");
        var descriptor = VBAddressableStorage.GetStringDescriptorNativeAddress(storage);

        Assert.AreEqual("abc", VBAddressableStorage.ReadStringDescriptor(descriptor));
        VBAddressableStorage.WriteStringDescriptor(descriptor, "xy");
        Assert.AreEqual("xy", VBAddressableStorage.ReadString(storage));
        Assert.AreEqual("xy", Marshal.PtrToStringBSTR(Marshal.ReadIntPtr(descriptor)));

        VBAddressableStorage.Dispose(storage);
    }

    [TestMethod]
    public void BooleanFacade_UsesTheVb6TwoByteMinusOneTrueRepresentation()
    {
        var storage = VBAddressableStorage.CreateBoolean(true);
        var address = VBAddressableStorage.GetBooleanNativeAddress(storage);

        Assert.AreEqual((short)-1, Marshal.ReadInt16(address));
        Marshal.WriteInt16(address, 1);
        ForceFullCollection();
        Assert.IsTrue(VBAddressableStorage.ReadBoolean(storage));

        VBAddressableStorage.WriteBoolean(storage, false);
        Assert.AreEqual((short)0, Marshal.ReadInt16(address));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadBoolean(storage));
    }

    [TestMethod]
    public void SingleFacade_PreservesTheFourByteIeee754NativeCellContract()
    {
        var storage = VBAddressableStorage.CreateSingle(1.5f);
        var address = VBAddressableStorage.GetSingleNativeAddress(storage);

        Assert.AreEqual(BitConverter.SingleToInt32Bits(1.5f), Marshal.ReadInt32(address));
        Marshal.WriteInt32(address, BitConverter.SingleToInt32Bits(2.5f));
        ForceFullCollection();
        Assert.AreEqual(2.5f, VBAddressableStorage.ReadSingle(storage));

        VBAddressableStorage.WriteSingle(storage, 3f);
        Assert.AreEqual(BitConverter.SingleToInt32Bits(3f), Marshal.ReadInt32(address));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadSingle(storage));
    }

    [TestMethod]
    public void DoubleFacade_PreservesTheEightByteIeee754NativeCellContract()
    {
        var storage = VBAddressableStorage.CreateDouble(1.5d);
        var address = VBAddressableStorage.GetDoubleNativeAddress(storage);

        Assert.AreEqual(BitConverter.DoubleToInt64Bits(1.5d), Marshal.ReadInt64(address));
        Marshal.WriteInt64(address, BitConverter.DoubleToInt64Bits(2.5d));
        ForceFullCollection();
        Assert.AreEqual(2.5d, VBAddressableStorage.ReadDouble(storage));

        VBAddressableStorage.WriteDouble(storage, 3d);
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(3d), Marshal.ReadInt64(address));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadDouble(storage));
    }

    [TestMethod]
    public void DateFacade_PreservesTheEightByteAutomationDateNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateDate(1.5d);
        var address = VBAddressableStorage.GetDateNativeAddress(storage);

        Assert.AreEqual(BitConverter.DoubleToInt64Bits(1.5d), Marshal.ReadInt64(address));
        Marshal.WriteInt64(address, BitConverter.DoubleToInt64Bits(2.5d));
        ForceFullCollection();
        Assert.AreEqual(2.5d, VBAddressableStorage.ReadDate(storage));

        VBAddressableStorage.WriteDate(storage, 3d);
        Assert.AreEqual(BitConverter.DoubleToInt64Bits(3d), Marshal.ReadInt64(address));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadDate(storage));
    }

    [TestMethod]
    public void CurrencyFacade_PreservesTheEightByteScaledInt64NativeCellContract()
    {
        var storage = VBAddressableStorage.CreateCurrency(VBCurrency.FromScaled(15_000));
        var address = VBAddressableStorage.GetCurrencyNativeAddress(storage);

        Assert.AreEqual(15_000L, Marshal.ReadInt64(address));
        Marshal.WriteInt64(address, 25_000);
        ForceFullCollection();
        Assert.AreEqual(VBCurrency.FromScaled(25_000), VBAddressableStorage.ReadCurrency(storage));

        VBAddressableStorage.WriteCurrency(storage, VBCurrency.FromScaled(30_000));
        Assert.AreEqual(30_000L, Marshal.ReadInt64(address));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadCurrency(storage));
    }

    [TestMethod]
    public void Int64Facade_PreservesTheEightByteSignedNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateInt64(72_623_859_790_382_856L);
        var address = VBAddressableStorage.GetInt64NativeAddress(storage);

        Assert.AreEqual(72_623_859_790_382_856L, Marshal.ReadInt64(address));
        Marshal.WriteInt64(address, 123L);
        ForceFullCollection();
        Assert.AreEqual(123L, VBAddressableStorage.ReadInt64(storage));

        VBAddressableStorage.WriteInt64(storage, 84_281_096L);
        Assert.AreEqual(84_281_096L, Marshal.ReadInt64(address));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadInt64(storage));
    }

    [TestMethod]
    public void UInt64Facade_PreservesTheEightByteUnsignedNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateUInt64(18_446_744_073_709_551_614UL);
        var address = VBAddressableStorage.GetUInt64NativeAddress(storage);

        Assert.AreEqual(18_446_744_073_709_551_614UL, unchecked((ulong)Marshal.ReadInt64(address)));
        Marshal.WriteInt64(address, 123L);
        ForceFullCollection();

        Assert.AreEqual(123UL, VBAddressableStorage.ReadUInt64(storage));
        VBAddressableStorage.WriteUInt64(storage, 10_000_000_000_000_000_000UL);
        Assert.AreEqual(10_000_000_000_000_000_000UL, unchecked((ulong)Marshal.ReadInt64(address)));

        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadUInt64(storage));
    }

    [TestMethod]
    public void IntPtr32Facade_PreservesTheX86FourByteNativeCellContract()
    {
        var storage = VBAddressableStorage.CreateIntPtr32(new IntPtr(16_909_060));
        var address = VBAddressableStorage.GetIntPtr32NativeAddress(storage);

        Assert.AreEqual(16_909_060, Marshal.ReadInt32(address));
        Marshal.WriteInt32(address, 123);
        ForceFullCollection();
        Assert.AreEqual(new IntPtr(123), VBAddressableStorage.ReadIntPtr32(storage));

        VBAddressableStorage.WriteIntPtr32(storage, new IntPtr(84_281_096));
        Assert.AreEqual(84_281_096, Marshal.ReadInt32(address));
        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadIntPtr32(storage));
    }

    [TestMethod]
    public void NullTolerantFacade_LeavesTheOrdinarySlotInChargeUntilAnAddressIsAsked()
    {
        // Die Zelle einer Modulvariablen entsteht erst beim ersten VarPtr. Solange sie fehlt,
        // muessen Lesen und Schreiben den gewoehnlichen Feldwert durchreichen statt zu werfen --
        // sonst braeuchte es einen Modulinitialisierer, den es ueber Modulgrenzen nicht gibt.
        Assert.AreEqual(7, VBAddressableStorage.ReadInt32Or(null, 7));
        VBAddressableStorage.WriteInt32IfPresent(null, 9);
        Assert.AreEqual("abc", VBAddressableStorage.ReadStringOr(null, "abc"));
        VBAddressableStorage.WriteStringIfPresent(null, "def");
        Assert.AreEqual(1.5d, VBAddressableStorage.ReadDoubleOr(null, 1.5d));
        Assert.AreEqual(VBCurrency.FromScaled(25_000L), VBAddressableStorage.ReadCurrencyOr(null, VBCurrency.FromScaled(25_000L)));
    }

    [TestMethod]
    public void NullTolerantFacade_SeedsOneCellAndThenKeepsIt()
    {
        var storage = VBAddressableStorage.EnsureInt32(null, 16_909_060);
        Assert.AreSame(storage, VBAddressableStorage.EnsureInt32(storage, 0));

        var address = VBAddressableStorage.GetInt32NativeAddress(storage);
        Assert.AreEqual(16_909_060, Marshal.ReadInt32(address));

        // Ab jetzt ist die Zelle massgeblich: Ein nativer Schreibzugriff kommt beim Lesen an,
        // und ein gewoehnlicher Schreibzugriff geht wieder in die Zelle.
        Marshal.WriteInt32(address, 123);
        ForceFullCollection();
        Assert.AreEqual(123, VBAddressableStorage.ReadInt32Or(storage, 0));

        VBAddressableStorage.WriteInt32IfPresent(storage, 84_281_096);
        Assert.AreEqual(84_281_096, Marshal.ReadInt32(address));

        VBAddressableStorage.Dispose(storage);
    }

    [TestMethod]
    public void NullTolerantStringFacade_SeedsOneBStrCellAndThenKeepsIt()
    {
        var storage = VBAddressableStorage.EnsureString(null, "abc");
        Assert.AreSame(storage, VBAddressableStorage.EnsureString(storage, "zzz"));
        Assert.AreEqual("abc", VBAddressableStorage.ReadStringOr(storage, "zzz"));

        VBAddressableStorage.WriteStringIfPresent(storage, "de");
        var address = VBAddressableStorage.GetStringNativeAddress(storage);
        Assert.AreEqual("de", Marshal.PtrToStringBSTR(address));

        VBAddressableStorage.Dispose(storage);
    }

    // Nachbau der Form, die der Emitter fuer ein VB6-Type erzeugt: sequentiell, Pack 4, und die
    // Member sind Assembly-sichtbar, nicht public. Genau daran ist die Offsetsuche zuerst
    // gescheitert, weil Type.GetField ohne NonPublic nichts findet.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct Innen
    {
        internal int A;
        internal int B;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct Punkt
    {
        internal int X;
        internal short Y;
        internal double Z;
        internal Innen Tief;
    }

    [TestMethod]
    public void RecordFacade_PlacesEveryMemberWhereTheMarshallerDoes()
    {
        var storage = VBAddressableStorage.CreateRecord(new Punkt { X = 16_909_060, Z = 1.5 });
        var address = VBAddressableStorage.GetRecordNativeAddress(storage);

        Assert.AreEqual(16_909_060, Marshal.ReadInt32(address));
        Assert.AreEqual(address, VBAddressableStorage.GetRecordMemberNativeAddress(storage, "X"));
        Assert.AreEqual(4L, Offset(storage, "Y"));
        Assert.AreEqual(8L, Offset(storage, "Z"));
        Assert.AreEqual(16L, Offset(storage, "Tief"));
        Assert.AreEqual(20L, Offset(storage, "Tief.B"));

        VBAddressableStorage.Dispose(storage);

        long Offset(object cell, string path) =>
            VBAddressableStorage.GetRecordMemberNativeAddress(cell, path).ToInt64() - address.ToInt64();
    }

    [TestMethod]
    public void RecordFacade_CarriesNativeWritesBackIntoTheManagedValue()
    {
        var storage = VBAddressableStorage.CreateRecord(new Punkt { X = 1 });
        var address = VBAddressableStorage.GetRecordNativeAddress(storage);

        Marshal.WriteInt32(address, 123);
        ForceFullCollection();
        Assert.AreEqual(123, ((Punkt)VBAddressableStorage.ReadRecord(storage)).X);

        VBAddressableStorage.WriteRecord(storage, new Punkt { X = 456 });
        Assert.AreEqual(456, Marshal.ReadInt32(address));

        // Die Zelle kennt genau einen Datensatztyp; ein anderer waere ein anderes Layout.
        Assert.ThrowsException<ArgumentException>(() =>
            VBAddressableStorage.WriteRecord(storage, new Innen { A = 1 }));

        VBAddressableStorage.Dispose(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadRecord(storage));
    }

    [TestMethod]
    public void NullTolerantRecordFacade_LeavesTheOrdinarySlotInChargeUntilAnAddressIsAsked()
    {
        var current = new Punkt { X = 7 };
        Assert.AreEqual(7, ((Punkt)VBAddressableStorage.ReadRecordOr(null, current)).X);
        VBAddressableStorage.WriteRecordIfPresent(null, current);

        var storage = VBAddressableStorage.EnsureRecord(null, current);
        Assert.AreSame(storage, VBAddressableStorage.EnsureRecord(storage, new Punkt { X = 9 }));
        Assert.AreEqual(7, ((Punkt)VBAddressableStorage.ReadRecordOr(storage, default(Punkt))).X);

        VBAddressableStorage.Dispose(storage);
    }

    [TestMethod]
    public void VariantCell_KeepsEverySubtypeDistinctInTheNativeVariant()
    {
        // Die Abnahmebedingung der Karte: Subtyp, Empty, Null, Nothing, BSTR und Fehlerwerte
        // bleiben unterscheidbar. In verwalteten Begriffen sind Empty, Null und Nothing alle
        // "kein Wert" -- eine Nullreferenz und zwei Marker --, nativ sind es drei verschiedene
        // VARIANTs. Gemessen wird deshalb das vt-Feld selbst, nicht nur der Rueckweg.
        var dataOffset = 8;

        AssertVariant(null, 0, cell => { });
        AssertVariant(VBVariants.NullValue(), 1, cell => { });
        AssertVariant(
            VBVariants.NothingValue(),
            9,
            address => Assert.AreEqual(IntPtr.Zero, Marshal.ReadIntPtr(address, dataOffset)));
        AssertVariant(
            "abc",
            8,
            address => Assert.AreEqual("abc", Marshal.PtrToStringBSTR(Marshal.ReadIntPtr(address, dataOffset))));
        AssertVariant(
            new VBErrorValue(9),
            10,
            address => Assert.AreEqual(9, Marshal.ReadInt32(address, dataOffset)));
        AssertVariant(
            VBVariants.MissingValue(),
            10,
            address => Assert.AreEqual(unchecked((int)0x80020004), Marshal.ReadInt32(address, dataOffset)));

        AssertVariant((short)7, 2, address => Assert.AreEqual((short)7, Marshal.ReadInt16(address, dataOffset)));
        AssertVariant(4711, 3, address => Assert.AreEqual(4711, Marshal.ReadInt32(address, dataOffset)));
        AssertVariant(true, 11, address => Assert.AreEqual((short)-1, Marshal.ReadInt16(address, dataOffset)));
        AssertVariant((byte)200, 17, address => Assert.AreEqual((byte)200, Marshal.ReadByte(address, dataOffset)));
        AssertVariant(
            2.5d,
            5,
            address => Assert.AreEqual(2.5d, BitConverter.Int64BitsToDouble(Marshal.ReadInt64(address, dataOffset))));

        // VT_UI4 statt der 20, die VarType meldet: VB6 kennt keinen vorzeichenlosen 32-Bit-
        // Variant, der Deskriptor muss trotzdem sagen, was die Nutzlast wirklich ist.
        AssertVariant(7u, 19, address => Assert.AreEqual(7, Marshal.ReadInt32(address, dataOffset)));
    }

    [TestMethod]
    public void VariantCell_RoundTripsAndReleasesItsPreviousString()
    {
        var cell = VBAddressableStorage.CreateVariant("erste");
        try
        {
            Assert.AreEqual("erste", VBAddressableStorage.ReadVariant(cell));

            // Eine Neuzuweisung muss die alte BSTR freigeben; VariantClear ist der Weg dafuer.
            // Sichtbar ist hier nur, dass der Deskriptor danach die neue Zeichenkette nennt --
            // ein Leck faellt erst als solches auf, wenn es jemand misst.
            VBAddressableStorage.WriteVariant(cell, "zweite");
            Assert.AreEqual("zweite", VBAddressableStorage.ReadVariant(cell));

            VBAddressableStorage.WriteVariant(cell, VBVariants.NullValue());
            Assert.AreSame(VBVariants.NullValue(), VBAddressableStorage.ReadVariant(cell));
            Assert.AreEqual(1, VBVariants.VarType(VBAddressableStorage.ReadVariant(cell)));

            // Die Adresse bleibt dieselbe -- ein gespeicherter Zeiger ueberlebt jede Zuweisung.
            var address = VBAddressableStorage.GetVariantNativeAddress(cell);
            VBAddressableStorage.WriteVariant(cell, 42);
            Assert.AreEqual(address, VBAddressableStorage.GetVariantNativeAddress(cell));
            Assert.AreEqual(42, VBAddressableStorage.ReadVariant(cell));
        }
        finally
        {
            ((IDisposable)cell).Dispose();
        }
    }

    [TestMethod]
    public void VariantCell_RefusesAValueWhoseStorageLivesBesideTheVariant()
    {
        // Ein Objekt, ein Array oder ein UDT besitzt Speicher neben dem VARIANT. Das ist ein
        // eigener Vertrag, und ein halb gefuellter Deskriptor waere schlechter als die Meldung.
        Assert.ThrowsException<NotSupportedException>(
            () => VBAddressableStorage.CreateVariant(new VBArray<int>(new VBArrayBound(0, 1))));
    }

    private static void AssertVariant(object? value, short expectedVarType, Action<IntPtr> inspect)
    {
        var cell = VBAddressableStorage.CreateVariant(value);
        try
        {
            var address = VBAddressableStorage.GetVariantNativeAddress(cell);
            Assert.AreEqual(
                expectedVarType,
                Marshal.ReadInt16(address),
                $"vt für '{value ?? "Empty"}'");
            inspect(address);

            var roundTripped = VBAddressableStorage.ReadVariant(cell);
            Assert.AreEqual(
                VBVariants.VarType(value),
                VBVariants.VarType(roundTripped),
                $"VarType nach dem Rückweg für '{value ?? "Empty"}'");
        }
        finally
        {
            ((IDisposable)cell).Dispose();
        }
    }

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
    }
}
