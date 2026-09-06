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

        VBAddressableStorage.DisposeInt32(storage);
        Assert.ThrowsException<ObjectDisposedException>(() => VBAddressableStorage.ReadInt32(storage));
    }

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
    }
}
