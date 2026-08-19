namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayTypeSymbolTests
{
    [TestMethod]
    public void ArrayTypeSymbol_PreservesElementTypeAndRank()
    {
        var oneDimensional = new ArrayTypeSymbol(TypeSymbol.Long, 1);
        var twoDimensional = new ArrayTypeSymbol(TypeSymbol.String, 2);

        Assert.AreEqual(TypeSymbol.Long, oneDimensional.ElementType);
        Assert.AreEqual(1, oneDimensional.Rank);
        Assert.AreEqual("Long()", oneDimensional.Name);
        Assert.AreEqual(TypeSymbol.String, twoDimensional.ElementType);
        Assert.AreEqual(2, twoDimensional.Rank);
        Assert.AreEqual("String(,)", twoDimensional.Name);
    }

    [TestMethod]
    public void ArrayTypeSymbol_RejectsNonPositiveRank()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ArrayTypeSymbol(TypeSymbol.Integer, 0));
    }
}