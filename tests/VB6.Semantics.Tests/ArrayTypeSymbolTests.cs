namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayTypeSymbolTests
{
    [TestMethod]
    public void ArrayTypeSymbol_PreservesElementTypeAndKnownRank()
    {
        var oneDimensional = new ArrayTypeSymbol(TypeSymbol.Long, 1);
        var twoDimensional = new ArrayTypeSymbol(TypeSymbol.String, 2);

        Assert.AreEqual(TypeSymbol.Long, oneDimensional.ElementType);
        Assert.AreEqual(1, oneDimensional.Rank);
        Assert.IsTrue(oneDimensional.HasKnownRank);
        Assert.AreEqual("Long()", oneDimensional.Name);
        Assert.AreEqual(TypeSymbol.String, twoDimensional.ElementType);
        Assert.AreEqual(2, twoDimensional.Rank);
        Assert.IsTrue(twoDimensional.HasKnownRank);
        Assert.AreEqual("String(,)", twoDimensional.Name);
    }

    [TestMethod]
    public void ArrayTypeSymbol_CanRepresentUnknownDynamicRank()
    {
        var dynamicArray = new ArrayTypeSymbol(TypeSymbol.Long);

        Assert.AreEqual(TypeSymbol.Long, dynamicArray.ElementType);
        Assert.IsNull(dynamicArray.Rank);
        Assert.IsFalse(dynamicArray.HasKnownRank);
        Assert.AreEqual("Long()", dynamicArray.Name);
    }

    [TestMethod]
    public void ArrayTypeSymbol_RejectsNonPositiveKnownRank()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ArrayTypeSymbol(TypeSymbol.Integer, 0));
    }
}
