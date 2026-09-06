using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime.Tests;

/// <summary>
/// The native reference count behind the slot ownership protocol, read as a number.
///
/// <see cref="ObjectLifetimeTests"/> proves the same protocol through its effects: a released RCW
/// throws, a member still answers, an out-of-process server exits. Those are the right tests for
/// "did the last owner let go", and they were all green while nobody could say how many native
/// references were taken on the way. A reference dropped twice and a reference dropped once look
/// identical from the outside as long as the object ends up gone.
///
/// These cases read the count instead. They need no registered component -- the identity under
/// test is <see cref="CountingComIdentity"/>, built and counted here -- so unlike the
/// Scripting.* cases they cannot quietly skip themselves on a machine that lacks a component.
///
/// Every count below was measured before it was asserted, and the implementation turned out to be
/// correct at every boundary the emitter actually produces. One case is different in kind: the
/// last one records where the contract *ends* rather than that it holds. Adoption spends a share
/// of a wrapper and presumes the value brought one of its own; that presumption is met by every
/// marshalled COM result, and the test says out loud what happens when it is not.
/// </summary>
[TestClass]
public sealed class ComReferenceCountTests
{
    private static bool SkipUnlessWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        Assert.Inconclusive("Native COM reference counting is a Windows contract.");
        return true;
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void Wrapper_TakesExactlyOneNativeReferenceAndGivesItBack()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        // The fixture's own contract first: without it every number below is unreadable.
        var identity = new CountingComIdentity();
        Assert.AreEqual(0, identity.References);

        var wrapper = identity.CreateRuntimeCallableWrapper();
        Assert.IsTrue(Marshal.IsComObject(wrapper));
        Assert.AreEqual(1, identity.References, "An RCW holds exactly one reference.");

        Marshal.FinalReleaseComObject(wrapper);
        Assert.AreEqual(0, identity.References);
        Assert.AreEqual(identity.AddRefCalls, identity.ReleaseCalls, "AddRef and Release must balance.");
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void DirectActivation_ReturnsTheNativeCountToZeroAtItsLastSlot()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();
        Assert.AreEqual(1, identity.References);

        object? slot = VBObjectLifetime.TransferComActivation(null, wrapper);
        Assert.AreEqual(2, identity.References, "Adoption adds the runtime's own IUnknown hold.");

        slot = VBObjectLifetime.Transfer(slot, null);
        Assert.IsNull(slot);
        Assert.AreEqual(0, identity.References, "The last owner releases the hold and the RCW share.");
        Assert.AreEqual(identity.AddRefCalls, identity.ReleaseCalls);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void Alias_TakesNoFurtherNativeReferenceAndHoldsUntilTheLastSlot()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();
        object? primary = VBObjectLifetime.TransferComActivation(null, wrapper);
        var adopted = identity.References;

        object? alias = VBObjectLifetime.Replace(null, primary);
        Assert.AreEqual(adopted, identity.References, "An alias is a VB6 owner, not a second native reference.");

        primary = VBObjectLifetime.Transfer(primary, null);
        Assert.AreEqual(adopted, identity.References, "The alias still owns the object.");

        alias = VBObjectLifetime.Transfer(alias, null);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void BorrowedWrapper_KeepsItsOwnNativeReferenceWhenVb6LetsGo()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();

        object? slot = VBObjectLifetime.Replace(null, wrapper);
        Assert.AreEqual(2, identity.References, "A borrowed object still gets a controlled IUnknown hold.");

        slot = VBObjectLifetime.Transfer(slot, null);

        // The difference that matters: releasing VB6 storage drops the runtime's hold and nothing
        // else. A host that handed us the wrapper still has a live object.
        Assert.AreEqual(1, identity.References, "The host's own reference must survive.");

        Marshal.FinalReleaseComObject(wrapper);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ForeignComMemberResult_IsAdoptedAndFullyReleased()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();

        object? slot = VBObjectLifetime.ReplaceComMemberResult(null, wrapper);
        Assert.AreEqual(2, identity.References, "An interface result carries a fresh COM reference.");

        slot = VBObjectLifetime.Transfer(slot, null);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void SelfAssignment_DoesNotDropANativeReference()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();
        object? slot = VBObjectLifetime.TransferComActivation(null, wrapper);
        var adopted = identity.References;

        slot = VBObjectLifetime.Replace(slot, slot);
        Assert.AreEqual(adopted, identity.References, "Retain must happen before the release of the replaced value.");

        slot = VBObjectLifetime.Transfer(slot, null);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ContainerStorage_HoldsTheObjectUntilItsLastContainer()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();

        var array = new VBArray<object>(new VBArrayBound(0, 1));
        array.TransferComActivationReference([0], wrapper);
        var adopted = identity.References;

        var collection = new VBCollection();
        VBCollection.AddComActivationValue(
            collection,
            wrapper,
            VBVariants.MissingValue(),
            VBVariants.MissingValue(),
            VBVariants.MissingValue());
        Assert.AreEqual(adopted, identity.References, "A second container is a second VB6 owner, not a second reference.");

        VBObjectLifetime.Release(array);
        Assert.AreEqual(adopted, identity.References, "The collection still owns the object.");

        VBObjectLifetime.Release(collection);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AdoptedObject_SurvivesGarbageCollection()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        object? slot = VBObjectLifetime.TransferComActivation(null, identity.CreateRuntimeCallableWrapper());
        var adopted = identity.References;

        for (var round = 0; round < 3; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // A finalized RCW would release the object while VB6 storage still refers to it -- the
        // failure this whole protocol exists to prevent, and one no reachability test can see.
        Assert.AreEqual(adopted, identity.References, "A collection must not release a live VB6 reference.");

        slot = VBObjectLifetime.Transfer(slot, null);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void RepeatedRetain_TakesOneNativeHoldAndReleasesItOnce()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();

        VBObjectLifetime.Retain(wrapper);
        VBObjectLifetime.Retain(wrapper);
        VBObjectLifetime.Retain(wrapper);
        Assert.AreEqual(2, identity.References, "Three VB6 owners share one native hold.");

        VBObjectLifetime.Release(wrapper);
        VBObjectLifetime.Release(wrapper);
        Assert.AreEqual(2, identity.References);

        VBObjectLifetime.Release(wrapper);
        Assert.AreEqual(1, identity.References, "The hold goes at the last VB6 owner, the borrowed wrapper stays.");

        Marshal.FinalReleaseComObject(wrapper);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ReleaseWithoutRetain_DoesNotOverRelease()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var wrapper = identity.CreateRuntimeCallableWrapper();

        // An over-release is the one failure that cannot be observed after the fact: the object is
        // gone, and whoever still held it finds out at a call site far away from the cause.
        VBObjectLifetime.Release(wrapper);
        Assert.AreEqual(1, identity.References, "A release without a matching retain must do nothing.");

        Marshal.FinalReleaseComObject(wrapper);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void SecondHandout_SharesTheWrapperWithoutASecondNativeReference()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        // The fact the cases below rest on. Marshalling the same identity again returns the same
        // RCW and adds a share of it, not a native reference -- and ReleaseComObject spends one
        // share, releasing the object only once the last one is gone.
        var identity = new CountingComIdentity();
        var first = identity.CreateRuntimeCallableWrapper();
        var second = identity.CreateRuntimeCallableWrapper();

        Assert.AreSame(first, second, "The CLR caches one wrapper per identity.");
        Assert.AreEqual(1, identity.References, "A second handout is a share, not a reference.");

        Marshal.ReleaseComObject(second);
        Assert.AreEqual(1, identity.References, "One share left, so the object stays.");

        Marshal.ReleaseComObject(first);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void MemberResultWithItsOwnHandout_LeavesAManagedHolderIntact()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        // What a real COM member return looks like: the holder has its share, marshalling the
        // result produces another, and VB6 adopts that one.
        var identity = new CountingComIdentity();
        var holderWrapper = identity.CreateRuntimeCallableWrapper();
        var memberResult = identity.CreateRuntimeCallableWrapper();

        object? slot = VBObjectLifetime.ReplaceComMemberResult(null, memberResult);
        slot = VBObjectLifetime.Transfer(slot, null);

        Assert.AreEqual(1, identity.References, "The holder's share must survive VB6 letting go.");

        Marshal.FinalReleaseComObject(holderWrapper);
        Assert.AreEqual(0, identity.References);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TwoAdoptedMemberResults_LeaveAManagedHolderIntact()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        var identity = new CountingComIdentity();
        var holderWrapper = identity.CreateRuntimeCallableWrapper();

        object? first = VBObjectLifetime.ReplaceComMemberResult(null, identity.CreateRuntimeCallableWrapper());
        object? second = VBObjectLifetime.ReplaceComMemberResult(null, identity.CreateRuntimeCallableWrapper());

        first = VBObjectLifetime.Transfer(first, null);
        second = VBObjectLifetime.Transfer(second, null);

        Assert.AreEqual(1, identity.References, "Two adoptions spend two shares, not the holder's.");
        Assert.AreEqual(0, Marshal.ReleaseComObject(holderWrapper), "The holder still owned the last share.");
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AdoptingAHoldersOwnWrapper_TakesThatWrapperOverEntirely()
    {
        if (SkipUnlessWindows())
        {
            return;
        }

        // The boundary of the ownership contract, written down because it is invisible in the
        // code. Adoption spends a share of the wrapper and presumes the adopted value arrived with
        // one of its own -- which every marshalled COM result does. Hand it the very object a
        // managed holder is using, with no share of its own, and VB6's release takes the holder's
        // wrapper with it.
        //
        // No emitted path reaches this today: a value loaded back out of VB6 storage is classified
        // as borrowed, and every adopting path goes through marshalling and so brings its own
        // share. The case is here so a future path that does reach it fails here, loudly, instead
        // of killing somebody else's object at a call site far away from the cause.
        var identity = new CountingComIdentity();
        var holderWrapper = identity.CreateRuntimeCallableWrapper();

        object? slot = VBObjectLifetime.ReplaceComMemberResult(null, holderWrapper);
        slot = VBObjectLifetime.Transfer(slot, null);

        Assert.AreEqual(0, identity.References, "The single share was spent, so nothing is left.");
        Assert.ThrowsExactly<InvalidComObjectException>(
            () => Marshal.GetIUnknownForObject(holderWrapper),
            "The holder's wrapper is gone -- this is why adoption needs a share of its own.");
    }
}
