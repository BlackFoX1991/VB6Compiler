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

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
    }
}
