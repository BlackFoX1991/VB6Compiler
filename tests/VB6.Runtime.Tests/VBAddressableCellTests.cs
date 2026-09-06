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

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
    }
}
