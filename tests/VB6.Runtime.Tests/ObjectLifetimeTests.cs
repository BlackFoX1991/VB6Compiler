using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class ObjectLifetimeTests
{
    private sealed class Terminable
    {
        public int Runs { get; private set; }

        // The emitted name a generated class carries. Looking for the plain "Class_Terminate" was
        // the bug this test exists to prevent: it found nothing, and nothing is what happened.
        private void __vb6_Class_Terminate() => Runs++;
    }

    private sealed class Throwing
    {
        private void __vb6_Class_Terminate() => throw new InvalidOperationException("boom");
    }

    private sealed class AddressableOwner
    {
        public object? Cell;
    }

    [TestMethod]
    public void RunTerminator_RunsTheTerminatorOnce()
    {
        var instance = new Terminable();

        VBObjectLifetime.Register(instance);
        VBObjectLifetime.RunTerminator(instance);
        VBObjectLifetime.RunTerminator(instance);

        Assert.AreEqual(1, instance.Runs);
    }

    [TestMethod]
    public void RunPendingTerminators_RunsAnInstanceThatIsStillAlive()
    {
        var instance = new Terminable();

        VBObjectLifetime.Register(instance);
        VBObjectLifetime.RunPendingTerminators();

        Assert.AreEqual(1, instance.Runs);
        GC.KeepAlive(instance);
    }

    [TestMethod]
    public void RunPendingTerminators_DoesNotRunAnInstanceThatAlreadyTerminated()
    {
        var instance = new Terminable();

        VBObjectLifetime.Register(instance);
        VBObjectLifetime.RunTerminator(instance);
        VBObjectLifetime.RunPendingTerminators();

        Assert.AreEqual(1, instance.Runs);
        GC.KeepAlive(instance);
    }

    [TestMethod]
    public void RunPendingTerminators_DrainsNewestFirst()
    {
        // Nesting usually follows creation order, so an object is torn down before the objects it
        // was built from.
        List<string> order = [];
        var first = new Ordered("erst", order);
        var second = new Ordered("dann", order);

        VBObjectLifetime.Register(first);
        VBObjectLifetime.Register(second);
        VBObjectLifetime.RunPendingTerminators();

        CollectionAssert.AreEqual(new[] { "dann", "erst" }, order);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [TestMethod]
    public void RunTerminator_SwallowsAnErrorRaisedDuringTeardown()
    {
        var instance = new Throwing();

        VBObjectLifetime.Register(instance);

        // A terminator runs while the program is already ending; on the finalizer thread an
        // escaping exception would take the process down, which no VB6 program does.
        VBObjectLifetime.RunTerminator(instance);
    }

    [TestMethod]
    public void RunTerminator_IgnoresNothing()
    {
        VBObjectLifetime.Register(null);
        VBObjectLifetime.RunTerminator(null);
    }

    [TestMethod]
    public void RunTerminator_ReleasesAnAddressableFieldCellWithoutAUserTerminator()
    {
        var owner = new AddressableOwner { Cell = VBAddressableStorage.CreateInt32(42) };
        var cell = owner.Cell;

        VBObjectLifetime.Register(owner);
        VBObjectLifetime.RunTerminator(owner);
        VBObjectLifetime.RunTerminator(owner);

        Assert.IsNull(owner.Cell, "The object must not retain a dangling native cell after teardown.");
        Assert.ThrowsExactly<ObjectDisposedException>(
            () => VBAddressableStorage.GetInt32NativeAddress(cell!));
    }

    [TestMethod]
    public void Replace_KeepsAnAliasAliveUntilItsLastStorageOwnerLeaves()
    {
        var instance = new Terminable();
        VBObjectLifetime.Register(instance);

        // The constructor reference moves into the first slot. Copying it into alias retains
        // before the original slot is released, which is the order a Set assignment needs.
        object? first = VBObjectLifetime.Transfer(null, instance);
        object? alias = VBObjectLifetime.Replace(null, first);
        first = VBObjectLifetime.Transfer(first, null);
        Assert.AreEqual(0, instance.Runs);

        alias = VBObjectLifetime.Transfer(alias, null);
        Assert.AreEqual(1, instance.Runs);
    }

    [TestMethod]
    public void Replace_RetainsBeforeReleasingASelfAssignment()
    {
        var instance = new Terminable();
        VBObjectLifetime.Register(instance);

        object? slot = VBObjectLifetime.Transfer(null, instance);
        slot = VBObjectLifetime.Replace(slot, slot);
        Assert.AreEqual(0, instance.Runs);

        slot = VBObjectLifetime.Transfer(slot, null);
        Assert.AreEqual(1, instance.Runs);
    }

    [TestMethod]
    public void ReplaceComMemberResult_RetainsALateBoundManagedObject()
    {
        var instance = new Terminable();
        VBObjectLifetime.Register(instance);

        object? source = VBObjectLifetime.Transfer(null, instance);
        object? memberResult = VBObjectLifetime.ReplaceComMemberResult(null, source);
        source = VBObjectLifetime.Transfer(source, null);

        Assert.AreEqual(0, instance.Runs);

        memberResult = VBObjectLifetime.Transfer(memberResult, null);
        Assert.AreEqual(1, instance.Runs);
    }

    [TestMethod]
    public void Release_ReleasesTheReferencesOwnedByAnArrayStorage()
    {
        var instance = new Terminable();
        var values = new VBArray<object?>(new VBArrayBound(0, 0));
        VBObjectLifetime.Register(instance);
        ((IVBArray)values).TransferObjectValue([0], instance);

        // A copied array descriptor is another owner of every reference in the array. The first
        // descriptor leaving is therefore not enough to terminate its element.
        VBObjectLifetime.Retain(values);
        VBObjectLifetime.Release(values);
        Assert.AreEqual(0, instance.Runs);

        VBObjectLifetime.Release(values);
        Assert.AreEqual(1, instance.Runs);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void Transfer_ReleasesAnActivatedComObjectAfterItsLastStorageOwnerLeaves()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("RCW ownership is a Windows COM contract.");
            return;
        }

        if (Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false) is null)
        {
            Assert.Inconclusive("The Scripting.Dictionary COM class is not available.");
            return;
        }

        var dictionary = VBInteraction.CreateObject("Scripting.Dictionary", string.Empty);
        Assert.IsTrue(Marshal.IsComObject(dictionary));

        object? first = VBObjectLifetime.TransferComActivation(null, dictionary);
        object? alias = VBObjectLifetime.Replace(null, first);
        first = VBObjectLifetime.Transfer(first, null);
        Assert.AreEqual(0, Convert.ToInt32(VBDynamicDispatch.GetMember(alias, "Count")));

        alias = VBObjectLifetime.Transfer(alias, null);

        Assert.ThrowsExactly<InvalidComObjectException>(() => VBDynamicDispatch.GetMember(dictionary, "Count"));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TransferContainers_ReleaseActivatedComObjectsWhenTheirStorageLeaves()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("RCW ownership is a Windows COM contract.");
            return;
        }

        if (Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false) is null)
        {
            Assert.Inconclusive("The Scripting.Dictionary COM class is not available.");
            return;
        }

        var arrayDictionary = VBInteraction.CreateObject("Scripting.Dictionary", string.Empty);
        var values = new VBArray<object?>(new VBArrayBound(0, 0));
        values.TransferComActivationReference([0], arrayDictionary);
        VBObjectLifetime.Release(values);

        Assert.ThrowsExactly<InvalidComObjectException>(() => VBDynamicDispatch.GetMember(arrayDictionary, "Count"));

        var collectionDictionary = VBInteraction.CreateObject("Scripting.Dictionary", string.Empty);
        var collection = VBCollection.Create();
        var missing = VBVariants.MissingValue();
        VBCollection.AddComActivationValue(collection, collectionDictionary, missing, missing, missing);
        VBObjectLifetime.Release(collection);

        Assert.ThrowsExactly<InvalidComObjectException>(() => VBDynamicDispatch.GetMember(collectionDictionary, "Count"));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void Transfer_PreservesTheComOwnershipAlreadyHandedThroughAFunctionReturn()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("RCW ownership is a Windows COM contract.");
            return;
        }

        if (Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false) is null)
        {
            Assert.Inconclusive("The Scripting.Dictionary COM class is not available.");
            return;
        }

        var dictionary = VBInteraction.CreateObject("Scripting.Dictionary", string.Empty);
        object? functionLocal = VBObjectLifetime.TransferComActivation(null, dictionary);

        // Returning from a generated function retains the result before its local cleanup. The
        // caller's Transfer moves that established storage reference; it must not adopt the raw
        // RCW a second time.
        VBObjectLifetime.Retain(functionLocal);
        functionLocal = VBObjectLifetime.Transfer(functionLocal, null);
        object? caller = VBObjectLifetime.Transfer(null, dictionary);
        caller = VBObjectLifetime.Transfer(caller, null);

        Assert.ThrowsExactly<InvalidComObjectException>(() => VBDynamicDispatch.GetMember(dictionary, "Count"));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void Transfer_ReleasesAnRcwReturnedByAForeignComMember()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("RCW ownership is a Windows COM contract.");
            return;
        }

        if (Type.GetTypeFromProgID("Scripting.FileSystemObject", throwOnError: false) is null)
        {
            Assert.Inconclusive("The Scripting.FileSystemObject COM class is not available.");
            return;
        }

        var fileSystemObject = VBInteraction.CreateObject("Scripting.FileSystemObject", string.Empty);
        object? owner = VBObjectLifetime.TransferComActivation(null, fileSystemObject);
        var drives = VBDynamicDispatch.GetMember(owner, "Drives");
        Assert.IsNotNull(drives);
        Assert.IsTrue(Marshal.IsComObject(drives!));

        object? result = VBObjectLifetime.ReplaceComMemberResult(null, drives);
        Assert.IsTrue(Convert.ToInt32(VBDynamicDispatch.GetMember(result, "Count")) >= 1);
        result = VBObjectLifetime.Transfer(result, null);

        Assert.ThrowsExactly<InvalidComObjectException>(() => VBDynamicDispatch.GetMember(drives, "Count"));

        owner = VBObjectLifetime.Transfer(owner, null);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void Replace_DoesNotInvalidateABorrowedHostComWrapper()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("RCW ownership is a Windows COM contract.");
            return;
        }

        if (Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false) is null)
        {
            Assert.Inconclusive("The Scripting.Dictionary COM class is not available.");
            return;
        }

        var dictionary = VBInteraction.CreateObject("Scripting.Dictionary", string.Empty);
        try
        {
            object? slot = VBObjectLifetime.Replace(null, dictionary);
            slot = VBObjectLifetime.Transfer(slot, null);

            Assert.AreEqual(0, Convert.ToInt32(VBDynamicDispatch.GetMember(dictionary, "Count")));
        }
        finally
        {
            Marshal.FinalReleaseComObject(dictionary);
        }
    }

    private sealed class Ordered(string name, List<string> order)
    {
        private void __vb6_Class_Terminate() => order.Add(name);
    }
}
