using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBArrayTests
{
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

    private sealed class MutableValue
    {
        public int Value { get; set; }
    }
}
