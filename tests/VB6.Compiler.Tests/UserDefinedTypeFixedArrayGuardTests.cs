namespace VB6.Compiler.Tests;

/// <summary>
/// A fixed array member is created against its declared bounds when the member is first touched.
/// That only works for element types whose storage the backend can produce on its own, so the
/// remaining cases stay reported as VB6S0046 rather than silently compiling to something else.
/// </summary>
[TestClass]
public sealed class UserDefinedTypeFixedArrayGuardTests
{
    [TestMethod]
    public void EmitManagedApplication_AllowsFixedPrimitiveArrayMember()
    {
        var output = VB6TestProgram.Run("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Sub Main()
                Dim value As Record
                value.Values(1) = 10
                Debug.Print value.Values(1)
            End Sub
            """, "test.bas");

        Assert.AreEqual("10", output.Trim());
    }

    /// <summary>
    /// A dynamic array member is allocated by ReDim rather than by the enclosing value, so it
    /// starts out unallocated and needs no declared bounds.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_AllowsDynamicArrayMember()
    {
        var output = VB6TestProgram.Run("""
            Type Record
                Values() As Long
            End Type

            Sub Main()
                Dim value As Record
                ReDim value.Values(1 To 2)
                value.Values(1) = 10
                Debug.Print value.Values(1)
            End Sub
            """, "test.bas");

        Assert.AreEqual("10", output.Trim());
    }

    [TestMethod]
    public void Lower_KeepsFixedLengthStringArrayMemberGuarded()
    {
        var lowering = VBCompilation.Create("""
            Type Record
                Names(1 To 2) As String * 5
            End Type

            Sub Main()
                Dim value As Record
            End Sub
            """, "test.bas").Lower();

        Assert.IsFalse(lowering.Success);
        Assert.IsTrue(lowering.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
    }

    [TestMethod]
    public void Lower_AllowsFixedUdtElementArrayMember()
    {
        var lowering = VBCompilation.Create("""
            Type Child
                Value As Long
            End Type

            Type Record
                Children(1 To 2) As Child
            End Type

            Sub Main()
                Dim value As Record
            End Sub
            """, "test.bas").Lower();

        Assert.IsTrue(
            lowering.Success,
            string.Join(Environment.NewLine, lowering.Diagnostics.Select(diagnostic => diagnostic.ToString())));
    }
}
