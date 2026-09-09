using System.Runtime.InteropServices;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBArrayTests
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSafeArrayBound
    {
        public NativeSafeArrayBound(uint elementCount, int lowerBound)
        {
            ElementCount = elementCount;
            LowerBound = lowerBound;
        }

        public readonly uint ElementCount;
        public readonly int LowerBound;
    }

    [DllImport("oleaut32.dll")]
    private static extern IntPtr SafeArrayCreate(
        ushort variantType,
        uint dimensionCount,
        [In] NativeSafeArrayBound[] bounds);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayPutElement(IntPtr safeArray, int[] indices, ref int value);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayAccessData(IntPtr safeArray, out IntPtr data);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayUnaccessData(IntPtr safeArray);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayDestroy(IntPtr safeArray);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayDestroyData(IntPtr safeArray);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayDestroyDescriptor(IntPtr safeArray);

    [TestMethod]
    public void Array_PreservesNonZeroLowerBounds()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 3));

        Assert.AreEqual(1, array.Rank);
        Assert.AreEqual(3, array.Length);
        Assert.AreEqual(1, array.LBound());
        Assert.AreEqual(3, array.UBound());

        array[1] = 10;
        array[3] = 30;
        Assert.AreEqual(10, array[1]);
        Assert.AreEqual(30, array[3]);
    }

    [TestMethod]
    public void Array_PreservesMultipleDimensions()
    {
        var array = new VBArray<string>(
            new VBArrayBound(0, 1),
            new VBArrayBound(5, 7));

        Assert.AreEqual(2, array.Rank);
        Assert.AreEqual(6, array.Length);
        Assert.AreEqual(0, array.LBound(1));
        Assert.AreEqual(1, array.UBound(1));
        Assert.AreEqual(5, array.LBound(2));
        Assert.AreEqual(7, array.UBound(2));

        array[1, 6] = "ok";
        Assert.AreEqual("ok", array[1, 6]);
    }

    [TestMethod]
    public void Array_StringElementsUseVbEmptyStringDefault()
    {
        var array = new VBArray<string>(new VBArrayBound(1, 2));

        Assert.AreEqual(string.Empty, array[1]);
        Assert.AreEqual(string.Empty, array[2]);
    }

    [TestMethod]
    public void Array_ClearResetsElementsAndPreservesBounds()
    {
        var array = new VBArray<int>(
            new VBArrayBound(-1, 1),
            new VBArrayBound(4, 5));
        array[-1, 4] = 10;
        array[1, 5] = 20;

        array.Clear();

        Assert.AreEqual(-1, array.LBound(1));
        Assert.AreEqual(1, array.UBound(1));
        Assert.AreEqual(4, array.LBound(2));
        Assert.AreEqual(5, array.UBound(2));
        Assert.AreEqual(0, array[-1, 4]);
        Assert.AreEqual(0, array[1, 5]);
    }

    [TestMethod]
    public void Array_ClearRestoresVbStringDefaults()
    {
        var array = new VBArray<string>(new VBArrayBound(0, 1));
        array[0] = "first";
        array[1] = "second";

        array.Clear();

        Assert.AreEqual(string.Empty, array[0]);
        Assert.AreEqual(string.Empty, array[1]);
    }

    [TestMethod]
    public void Array_IndexerCanBePassedByReference()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 2));
        array[1] = 10;

        Increment(ref array[1]);

        Assert.AreEqual(11, array[1]);
    }

    [TestMethod]
    public void VariantArrayElementReferenceCanBePassedByReference()
    {
        var array = new VBArray<object>(new VBArrayBound(0, 0));
        array[0] = "before";

        Replace(ref VBArrayOperations.GetElementReference(array, new[] { 0 }));

        Assert.AreEqual("changed", array[0]);
    }

    [TestMethod]
    public void ClrArraysFollowTheVariantArrayContract()
    {
        var array = Array.CreateInstance(typeof(int), new[] { 2 }, new[] { 1 });
        array.SetValue(10, 1);

        Assert.IsTrue(VBVariants.IsArray(array));
        Assert.IsFalse(VBVariants.IsObject(array));
        Assert.AreEqual("Long()", VBFunctions.TypeName(array));
        Assert.AreEqual((short)8195, VBVariants.VarType(array));
        Assert.AreEqual(1, VBArrayOperations.LBound(array));
        Assert.AreEqual(2, VBArrayOperations.UBound(array));
        Assert.AreEqual(10, VBArrayOperations.GetElement(array, new object?[] { 1 }));

        VBArrayOperations.SetElement(array, new object?[] { 2 }, 30);

        Assert.AreEqual(30, array.GetValue(2));
        Assert.ThrowsException<InvalidOperationException>(() =>
            VBArrayOperations.GetElementReference(array, new[] { 1 }));
    }

    [TestMethod]
    public void Array_ClonePreservesBoundsAndCreatesIndependentStorage()
    {
        var array = new VBArray<int>(
            new VBArrayBound(-2, 0),
            new VBArrayBound(4, 5));
        array[-2, 4] = 24;
        array[0, 5] = 5;

        var clone = array.Clone();
        clone[-2, 4] = 99;

        Assert.AreEqual(-2, clone.LBound(1));
        Assert.AreEqual(0, clone.UBound(1));
        Assert.AreEqual(4, clone.LBound(2));
        Assert.AreEqual(5, clone.UBound(2));
        Assert.AreEqual(24, array[-2, 4]);
        Assert.AreEqual(99, clone[-2, 4]);
        Assert.AreEqual(5, clone[0, 5]);
    }

    [TestMethod]
    public void Array_CloneCanRecursivelyCloneManagedElements()
    {
        var array = new VBArray<MutableValue>(new VBArrayBound(1, 1));
        array[1] = new MutableValue { Value = 10 };

        var clone = array.Clone(value => new MutableValue { Value = value.Value });
        clone[1].Value = 20;

        Assert.AreEqual(10, array[1].Value);
        Assert.AreEqual(20, clone[1].Value);
    }

    [TestMethod]
    public void Array_CloneAndReDimPreserveElementDescriptor()
    {
        var array = new VBArray<object>("Object", 9, new VBArrayBound(0, 1));

        var clone = array.Clone();
        var resized = array.ReDimPreserve(new VBArrayBound(0, 2));

        Assert.AreEqual("Object", clone.ElementTypeName);
        Assert.AreEqual((short)9, clone.ElementVarType);
        Assert.AreEqual("Object", resized.ElementTypeName);
        Assert.AreEqual((short)9, resized.ElementVarType);
    }

    [TestMethod]
    public void Array_CopyBackPreservesDestinationDescriptorWhenShapeChanges()
    {
        var target = new VBArray<object>("Object", 9, new VBArrayBound(0, 1));
        var source = Array.CreateInstance(typeof(object), new[] { 3 }, new[] { 0 });
        source.SetValue("first", 0);
        source.SetValue("last", 2);

        var result = VBArrayOperations.CopyBack(target, source)!;

        Assert.AreEqual("Object", result.ElementTypeName);
        Assert.AreEqual((short)9, result.ElementVarType);
        Assert.AreEqual("Object()", VBFunctions.TypeName(result));
        Assert.AreEqual((short)8201, VBVariants.VarType(result));
        Assert.AreEqual("last", result[2]);
    }

    [TestMethod]
    public void Array_ReDimPreserveCanGrowLastDimension()
    {
        var array = new VBArray<int>(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 6));
        array[1, 5] = 15;
        array[1, 6] = 16;
        array[2, 5] = 25;
        array[2, 6] = 26;

        var resized = array.ReDimPreserve(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 8));

        Assert.AreEqual(8, resized.Length);
        Assert.AreEqual(15, resized[1, 5]);
        Assert.AreEqual(16, resized[1, 6]);
        Assert.AreEqual(25, resized[2, 5]);
        Assert.AreEqual(26, resized[2, 6]);
        Assert.AreEqual(0, resized[1, 7]);
        Assert.AreEqual(0, resized[2, 8]);
    }

    [TestMethod]
    public void Array_ReDimPreserveCanShrinkLastDimension()
    {
        var array = new VBArray<int>(new VBArrayBound(-1, 2));
        array[-1] = 10;
        array[0] = 20;
        array[1] = 30;
        array[2] = 40;

        var resized = array.ReDimPreserve(new VBArrayBound(-1, 0));

        Assert.AreEqual(-1, resized.LBound());
        Assert.AreEqual(0, resized.UBound());
        Assert.AreEqual(10, resized[-1]);
        Assert.AreEqual(20, resized[0]);
        Assert.ThrowsException<IndexOutOfRangeException>(() => _ = resized[1]);
    }

    [TestMethod]
    public void Array_ReDimPreserveRejectsRankChange()
    {
        var array = new VBArray<int>(new VBArrayBound(0, 2));

        Assert.ThrowsException<ArgumentException>(() => array.ReDimPreserve(
            new VBArrayBound(0, 2),
            new VBArrayBound(0, 2)));
    }

    [TestMethod]
    public void Array_ReDimPreserveRejectsEarlierDimensionChange()
    {
        var array = new VBArray<int>(
            new VBArrayBound(0, 1),
            new VBArrayBound(0, 2));

        Assert.ThrowsException<ArgumentException>(() => array.ReDimPreserve(
            new VBArrayBound(0, 2),
            new VBArrayBound(0, 3)));
    }

    [TestMethod]
    public void Array_ReDimPreserveRejectsLowerBoundChange()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 3));

        Assert.ThrowsException<ArgumentException>(() =>
            array.ReDimPreserve(new VBArrayBound(0, 3)));
    }

    [TestMethod]
    public void Array_RejectsOutOfRangeSubscriptsAndDimensions()
    {
        var array = new VBArray<int>(new VBArrayBound(-2, 2));

        Assert.ThrowsException<IndexOutOfRangeException>(() => _ = array[3]);
        Assert.ThrowsException<IndexOutOfRangeException>(() => _ = array[0, 0]);
        Assert.ThrowsException<IndexOutOfRangeException>(() => array.LBound(2));
    }

    [TestMethod]
    public void Array_RejectsInvalidBounds()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new VBArray<int>(new VBArrayBound(5, 4)));
        Assert.ThrowsException<ArgumentException>(() => new VBArray<int>());
    }

    [TestMethod]
    public void Array_AllowsTheZeroLengthParameterArrayShape()
    {
        var array = new VBArray<int>(new VBArrayBound(0, -1));

        Assert.AreEqual(0, array.Length);
        Assert.AreEqual(0, array.LBound());
        Assert.AreEqual(-1, array.UBound());
        CollectionAssert.AreEqual(Array.Empty<int>(), array.EnumerateValues().ToArray());
    }

    [TestMethod]
    public void ArrayBounds_RejectNonArrayVariantsWithTypeMismatch()
    {
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBArrayOperations.LBound(42));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBArrayOperations.UBound(42));
    }

    private static void Increment(ref int value) => value++;

    private static void Replace(ref object? value) => value = "changed";

    [TestMethod]
    public void CopyAssignedValue_GivesAnArrayItsOwnStorageAndLeavesEverythingElseAlone()
    {
        var source = new VBArray<object>(new VBArrayBound(2, 3));
        source[2] = "erst";
        source[3] = 7;

        var copy = (VBArray<object>)VBArrayOperations.CopyAssignedValue(source)!;
        copy[2] = "geaendert";

        Assert.AreEqual("erst", source[2]);
        Assert.AreEqual("geaendert", copy[2]);
        Assert.AreEqual(2, copy.LBound());
        Assert.AreEqual(3, copy.UBound());
        Assert.AreEqual(7, copy[3]);

        // Ein CLR-Array aus einem SAFEARRAY laeuft ueber denselben Vertrag.
        var clrArray = new[] { 1, 2, 3 };
        var clrCopy = (int[])VBArrayOperations.CopyAssignedValue(clrArray)!;
        clrCopy[0] = 42;
        Assert.AreEqual(1, clrArray[0]);
        Assert.AreEqual(42, clrCopy[0]);

        // Objekte behalten ihre Identitaet, Skalare gehen unveraendert durch.
        var instance = new object();
        Assert.AreSame(instance, VBArrayOperations.CopyAssignedValue(instance));
        Assert.AreSame(VBVariants.NothingValue(), VBArrayOperations.CopyAssignedValue(VBVariants.NothingValue()));
        Assert.AreEqual("text", VBArrayOperations.CopyAssignedValue("text"));
        Assert.IsNull(VBArrayOperations.CopyAssignedValue(null));
    }

    [TestMethod]
    public void ElementAddress_LaysTheElementsOutContiguouslyAndStopsTheCollectorMovingThem()
    {
        var array = new VBArray<int>(new VBArrayBound(5, 8));
        array[5] = 16_909_060;

        var first = array.GetElementNativeAddress(5);
        Assert.AreNotEqual(IntPtr.Zero, first);
        Assert.AreEqual(4L, array.GetElementNativeAddress(6).ToInt64() - first.ToInt64());
        Assert.AreEqual(12L, array.GetElementNativeAddress(8).ToInt64() - first.ToInt64());
        Assert.AreEqual(16_909_060, Marshal.ReadInt32(first));

        // Der Speicher darf sich nicht mehr bewegen, und es gibt keinen zweiten: Was ueber die
        // gewoehnliche Elementreferenz geschrieben wird, steht sofort an der Adresse.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        Assert.AreEqual(first, array.GetElementNativeAddress(5));

        array[5] = 4711;
        Assert.AreEqual(4711, Marshal.ReadInt32(first));
        Marshal.WriteInt32(first, 99);
        Assert.AreEqual(99, array[5]);
    }

    [TestMethod]
    public void ElementAddress_UsesSafeArrayOrderForEveryScalarDimension()
    {
        var rectangular = new VBArray<int>(
            new VBArrayBound(5, 6),
            new VBArrayBound(10, 12));
        rectangular[5, 10] = 1;
        rectangular[6, 10] = 2;
        rectangular[5, 11] = 3;

        var first = rectangular.GetElementNativeAddress(5, 10);
        Assert.AreEqual(4L, rectangular.GetElementNativeAddress(6, 10).ToInt64() - first.ToInt64());
        Assert.AreEqual(8L, rectangular.GetElementNativeAddress(5, 11).ToInt64() - first.ToInt64());
        Assert.AreEqual(1, Marshal.ReadInt32(first));
        Assert.AreEqual(2, Marshal.ReadInt32(first, sizeof(int)));
        Assert.AreEqual(3, Marshal.ReadInt32(first, 2 * sizeof(int)));

        Marshal.WriteInt32(first, 2 * sizeof(int), 99);
        Assert.AreEqual(99, rectangular[5, 11]);

        var descriptor = rectangular.GetSafeArrayNativeAddress();
        Assert.AreEqual(2, Marshal.ReadInt16(descriptor));
        Assert.AreEqual(sizeof(int), Marshal.ReadInt32(descriptor, sizeof(int)));
        var dataOffset = IntPtr.Size == sizeof(long) ? 16 : 12;
        Assert.AreEqual(first, Marshal.ReadIntPtr(descriptor, dataOffset));
        var boundsOffset = dataOffset + IntPtr.Size;
        // OleAut32 stores the descriptor bounds rightmost first even though its API takes source
        // indices leftmost first.  Reading the bytes makes that otherwise invisible ABI turn
        // explicit and keeps the element-stride assertion above independent from this header.
        Assert.AreEqual(3, Marshal.ReadInt32(descriptor, boundsOffset));
        Assert.AreEqual(10, Marshal.ReadInt32(descriptor, boundsOffset + sizeof(int)));
        Assert.AreEqual(2, Marshal.ReadInt32(descriptor, boundsOffset + 2 * sizeof(int)));
        Assert.AreEqual(5, Marshal.ReadInt32(descriptor, boundsOffset + 3 * sizeof(int)));

        // Ein Referenzelement hat gar kein flaches Layout.
        var references = new VBArray<string>(new VBArrayBound(0, 1));
        Assert.ThrowsException<NotSupportedException>(() => references.GetElementNativeAddress(0));
    }

    [TestMethod]
    public void ElementAddress_RefusesASubtypeThatIsWiderThanItsClrStorage()
    {
        // VB6 Boolean ist VT_BOOL, und der Deskriptor wuerde damit zwei Byte je Element ueber
        // einem ein Byte breiten CLR-bool[] versprechen. Ein nativer Leser laeuft dann ueber das
        // Ende des Puffers hinaus -- lautlos, weil ihn auf keiner Seite jemand prueft. Deshalb
        // antwortet weder ein Element noch der Deskriptor.
        var declared = new VBArray<bool>(null, (short)VarEnum.VT_BOOL, new VBArrayBound(0, 3));
        Assert.ThrowsException<NotSupportedException>(() => declared.GetElementNativeAddress(0));
        Assert.ThrowsException<NotSupportedException>(() => declared.GetSafeArrayNativeAddress());

        // Ohne deklarierten Subtyp gibt es fuer bool ueberhaupt keine Zuordnung. Der Weg ueber die
        // oeffentliche Runtime-API endet genauso, denn der Schutz sitzt hier und nicht im Lowerer.
        var undeclared = new VBArray<bool>(new VBArrayBound(0, 3));
        Assert.ThrowsException<NotSupportedException>(() => undeclared.GetElementNativeAddress(0));
        Assert.ThrowsException<NotSupportedException>(
            () => VBArrayOperations.ElementNativeAddress(undeclared, 0));
    }

    [TestMethod]
    public void ElementAddress_AcceptsASubtypeWhoseWidthMatchesItsClrStorage()
    {
        // Gegenprobe zur Breitenregel: VT_DATE ist ein eigener Subtyp ueber demselben acht Byte
        // breiten Double, VT_I2 einer ueber Short. Beide muessen weiter antworten -- sonst haette
        // die Pruefung nur gelernt, alles Ungewohnte abzulehnen.
        var dates = new VBArray<double>(null, (short)VarEnum.VT_DATE, new VBArrayBound(0, 1));
        Assert.AreEqual(
            (long)sizeof(double),
            dates.GetElementNativeAddress(1).ToInt64() - dates.GetElementNativeAddress(0).ToInt64());

        var integers = new VBArray<short>(null, (short)VarEnum.VT_I2, new VBArrayBound(0, 1));
        Assert.AreEqual(
            (long)sizeof(short),
            integers.GetElementNativeAddress(1).ToInt64() -
                integers.GetElementNativeAddress(0).ToInt64());
    }

    [TestMethod]
    public void WindowsAutomation_DeclaresATwoByteElementForVtBool()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The VT_BOOL element width is a Windows Automation contract.");
            return;
        }

        // Die Ablehnung oben steht auf zwei gemessenen Zahlen, nicht auf einer Annahme: was
        // OleAut32 als cbElements fuer VT_BOOL eintraegt, und wie breit ein CLR-bool[] wirklich
        // ist. Erst der Unterschied macht den Deskriptor unbrauchbar.
        var safeArray = SafeArrayCreate((ushort)VarEnum.VT_BOOL, 1, [new NativeSafeArrayBound(2, 0)]);
        Assert.AreNotEqual(IntPtr.Zero, safeArray);
        try
        {
            // cbElements steht hinter cDims und fFeatures.
            Assert.AreEqual(2, Marshal.ReadInt32(safeArray, 2 * sizeof(short)));
        }
        finally
        {
            Assert.AreEqual(0, SafeArrayDestroy(safeArray));
        }

        var managed = GC.AllocateArray<bool>(2, pinned: true);
        Assert.AreEqual(
            1L,
            Marshal.UnsafeAddrOfPinnedArrayElement(managed, 1).ToInt64() -
                Marshal.UnsafeAddrOfPinnedArrayElement(managed, 0).ToInt64());
    }

    [TestMethod]
    public void WindowsAutomation_ClearsStaticDataOnDestroyButNotOnDestroyDescriptor()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("SAFEARRAY teardown is a Windows Automation contract.");
            return;
        }

        // Die Freigabe von VBArray<T> stand zuerst auf der Annahme, FADF_STATIC halte OleAut32
        // von den Nutzdaten fern. Diese Messung hat sie widerlegt: Das Flag beschreibt den
        // Besitz, es schuetzt den Puffer nicht. SafeArrayDestroy nullt die Daten auch mit
        // gesetztem Flag -- und pvData zeigt bei uns auf ein gepinntes CLR-Array, also haette ein
        // Destroy den Inhalt eines noch lebenden VB6-Arrays stillschweigend geloescht.
        //
        // Beide Richtungen stehen hier, weil erst der Unterschied die Regel belegt. Wer die
        // Freigabe anfasst, sieht an diesem Test, warum es SafeArrayDestroyDescriptor sein muss.
        CollectionAssert.AreEqual(
            new[] { 0, 0, 0, 0 },
            MeasurePinnedDataAfterTeardown(destroyDescriptorOnly: false),
            "SafeArrayDestroy schreibt in fremden Speicher, trotz FADF_STATIC.");

        CollectionAssert.AreEqual(
            new[] { 0x5A5A5A5A, 0x11111111, 0x22222222, 0x3C3C3C3C },
            MeasurePinnedDataAfterTeardown(destroyDescriptorOnly: true),
            "SafeArrayDestroyDescriptor gibt nur den Deskriptor frei.");
    }

    [TestMethod]
    public void Dispose_ReleasesTheDescriptorWithoutTouchingTheArrayContents()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("SAFEARRAY teardown is a Windows Automation contract.");
            return;
        }

        // Der Regressionsfall zur Messung darueber: Ein Array ueberlebt die Freigabe seines
        // Deskriptors. Die erste Fassung rief SafeArrayDestroy und haette hier lauter Nullen
        // gelesen -- ohne Ausnahme, ohne Fehlernummer, einfach weg.
        var array = new VBArray<int>(new VBArrayBound(1, 4));
        array[1] = 11;
        array[2] = 22;
        array[3] = 33;
        array[4] = 44;

        Assert.AreNotEqual(IntPtr.Zero, array.GetSafeArrayNativeAddress());
        array.Dispose();

        CollectionAssert.AreEqual(
            new[] { 11, 22, 33, 44 },
            array.EnumerateValues().ToArray());

        // Und der naechste Zeiger baut einen neuen Deskriptor, statt den freigegebenen zu nennen.
        var again = array.GetSafeArrayNativeAddress();
        Assert.AreNotEqual(IntPtr.Zero, again);
        Assert.AreEqual(22, Marshal.ReadInt32(array.GetElementNativeAddress(2)));
        array.Dispose();
    }

    /// <summary>
    /// Builds the same static descriptor over a pinned CLR buffer that <c>VBArray&lt;T&gt;</c>
    /// builds, tears it down one of the two ways, and reports what is left in the buffer.
    /// </summary>
    private static int[] MeasurePinnedDataAfterTeardown(bool destroyDescriptorOnly)
    {
        const ushort FadfStatic = 0x0002;
        var dataOffset = IntPtr.Size == sizeof(long) ? 16 : 12;

        var pinned = GC.AllocateArray<int>(4, pinned: true);
        pinned[0] = 0x5A5A5A5A;
        pinned[1] = 0x11111111;
        pinned[2] = 0x22222222;
        pinned[3] = 0x3C3C3C3C;

        var safeArray = SafeArrayCreate((ushort)VarEnum.VT_I4, 1, [new NativeSafeArrayBound(4, 0)]);
        Assert.AreNotEqual(IntPtr.Zero, safeArray);
        Assert.AreEqual(0, SafeArrayDestroyData(safeArray));

        var features = unchecked((ushort)Marshal.ReadInt16(safeArray, sizeof(short)));
        Marshal.WriteInt16(safeArray, sizeof(short), unchecked((short)(features | FadfStatic)));
        Marshal.WriteIntPtr(safeArray, dataOffset, Marshal.UnsafeAddrOfPinnedArrayElement(pinned, 0));

        Assert.AreEqual(
            0,
            destroyDescriptorOnly ? SafeArrayDestroyDescriptor(safeArray) : SafeArrayDestroy(safeArray));
        return pinned;
    }

    [TestMethod]
    public void ElementAddress_IsReachableThroughTheNonGenericEntryPoint()
    {
        object array = new VBArray<short>(new VBArrayBound(0, 3));
        Assert.AreEqual(
            2L,
            VBArrayOperations.ElementNativeAddress(array, 1).ToInt64() -
                VBArrayOperations.ElementNativeAddress(array, 0).ToInt64());

        var unallocated = Assert.ThrowsException<VB6RuntimeErrorException>(
            () => VBArrayOperations.ElementNativeAddress(null, 0));
        Assert.AreEqual(9, unallocated.Number);
        Assert.AreEqual("Subscript out of range", unallocated.Message);
        Assert.ThrowsException<ArgumentException>(
            () => VBArrayOperations.ElementNativeAddress("kein Array", 0));
    }

    [TestMethod]
    public void WindowsAutomation_SafeArrayDataAdvancesTheLeftmostDimensionFirst()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("SAFEARRAY layout is a Windows Automation contract.");
            return;
        }

        var safeArray = SafeArrayCreate(
            (ushort)VarEnum.VT_I4,
            2,
            [new NativeSafeArrayBound(2, 0), new NativeSafeArrayBound(3, 0)]);
        Assert.AreNotEqual(IntPtr.Zero, safeArray);
        try
        {
            var values = new[] { 1, 2, 3, 4, 5, 6 };
            for (var right = 0; right < 3; right++)
            {
                for (var left = 0; left < 2; left++)
                {
                    var value = values[left + right * 2];
                    Assert.AreEqual(0, SafeArrayPutElement(safeArray, [left, right], ref value));
                }
            }

            Assert.AreEqual(0, SafeArrayAccessData(safeArray, out var data));
            try
            {
                CollectionAssert.AreEqual(
                    values,
                    Enumerable.Range(0, values.Length)
                        .Select(index => Marshal.ReadInt32(data, index * sizeof(int)))
                        .ToArray());
            }
            finally
            {
                Assert.AreEqual(0, SafeArrayUnaccessData(safeArray));
            }
        }
        finally
        {
            Assert.AreEqual(0, SafeArrayDestroy(safeArray));
        }
    }

    private sealed class MutableValue
    {
        public int Value { get; set; }
    }
}
