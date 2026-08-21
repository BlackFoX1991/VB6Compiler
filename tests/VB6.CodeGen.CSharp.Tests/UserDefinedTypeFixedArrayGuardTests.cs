using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class UserDefinedTypeFixedArrayGuardTests
{
    [TestMethod]
    public void GenerateCSharp_AllowsFixedPrimitiveArrayMember()
    {
        var generation = VBCompilation.Create("""
            Type Record
                Values(1 To 2) As Long
            End Type

            Sub Main()
                Dim value As Record
                value.Values(1) = 10
                Debug.Print value.Values(1)
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
    }

    /// <summary>
    /// A dynamic array member is allocated by ReDim rather than by the enclosing value, so it is a
    /// plain field that starts out unallocated - and the clone deep-copies it, because VB6 copies a
    /// user-defined type by value.
    /// </summary>
    [TestMethod]
    public void GenerateCSharp_AllowsDynamicArrayMember()
    {
        var generation = VBCompilation.Create("""
            Type Record
                Values() As Long
            End Type

            Sub Main()
                Dim value As Record
                ReDim value.Values(1 To 2)
                value.Values(1) = 10
                Debug.Print value.Values(1)
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        StringAssert.Contains(generation.Source, "public VBArray<int> __vb6_member_Values;");
    }

    [TestMethod]
    public void GenerateCSharp_KeepsFixedLengthStringArrayMemberGuarded()
    {
        var generation = VBCompilation.Create("""
            Type Record
                Names(1 To 2) As String * 5
            End Type

            Sub Main()
                Dim value As Record
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
    }

    [TestMethod]
    public void GenerateCSharp_KeepsFixedUdtElementArrayMemberGuarded()
    {
        var generation = VBCompilation.Create("""
            Type Child
                Value As Long
            End Type

            Type Record
                Children(1 To 2) As Child
            End Type

            Sub Main()
                Dim value As Record
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"));
    }
}
