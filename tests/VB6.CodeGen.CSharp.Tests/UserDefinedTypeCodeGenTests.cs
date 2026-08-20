using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class UserDefinedTypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsUserDefinedTypeAndMemberAccess()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Name As String * 16
                Values(1 To 2) As Integer
            End Type

            Sub Main()
                Dim point As Point
                point.X = 10
                point.Name = "ABCDE"
                point.Values(1) = 20
                Debug.Print point.X
                Debug.Print point.Values(1)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "using System.Runtime.InteropServices;");
        StringAssert.Contains(source, "[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]");
        StringAssert.Contains(source, "private sealed class __vb6_type_Point");
        StringAssert.Contains(source, "public int __vb6_field_X;");
        StringAssert.Contains(source, "[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]");
        StringAssert.Contains(source, "public string __vb6_field_Name;");
        StringAssert.Contains(source, "public VBArray<short> __vb6_field_Values;");
        StringAssert.Contains(source, "__vb6_field_Name = VBStrings.FixedLength(string.Empty, checked((int)VBConversions.CLng(");
        StringAssert.Contains(source, "__vb6_field_Values = new VBArray<short>(new VBArrayBound(");
        StringAssert.Contains(source, "__vb6_type_Point __vb6_point = new __vb6_type_Point();");
        StringAssert.Contains(source, "__vb6_point.__vb6_field_X = VBConversions.CLng(");
        StringAssert.Contains(source, "__vb6_point.__vb6_field_Name = VBStrings.FixedLength(");
        StringAssert.Contains(source, "__vb6_point.__vb6_field_Values[");
        StringAssert.Contains(source, "] = VBConversions.CInt(");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_point.__vb6_field_X);");
    }

    [TestMethod]
    public void Generate_EmitsWithBlockImplicitMemberAccess()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Values(1 To 2) As Integer
            End Type

            Sub Main()
                Dim point As Point
                With point
                    .X = 10
                    .Values(1) = 20
                    Debug.Print .X
                    Debug.Print .Values(1)
                End With
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_point.__vb6_field_X = VBConversions.CLng(");
        StringAssert.Contains(source, "__vb6_point.__vb6_field_Values[");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_point.__vb6_field_X);");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_point.__vb6_field_Values[");
    }

    [TestMethod]
    public void Generate_ClonesUserDefinedTypeAssignmentsAndByValArguments()
    {
        var analysis = VBCompilation.Create("""
            Type Inner
                Value As Long
            End Type

            Type Outer
                Inner As Inner
            End Type

            Sub Mutate(ByVal item As Outer)
                item.Inner.Value = 99
            End Sub

            Sub Main()
                Dim first As Outer
                Dim second As Outer
                first.Inner.Value = 10
                second = first
                Mutate second
                Debug.Print second.Inner.Value
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "public __vb6_type_Outer Clone()");
        StringAssert.Contains(source, "copy.__vb6_field_Inner = __vb6_field_Inner.Clone();");
        StringAssert.Contains(source, "__vb6_second = __vb6_first.Clone();");
        StringAssert.Contains(source, "__vb6_Mutate(__vb6_second.Clone());");
    }

    [TestMethod]
    public void Generate_EmitsUserDefinedTypeFieldAndArrayFieldByRefArguments()
    {
        var analysis = VBCompilation.Create("""
            Type Point
                X As Long
                Values(1 To 1) As Long
            End Type

            Sub Main()
                Dim point As Point
                Call Update(point.X)
                Call Update(point.Values(1))
            End Sub

            Sub Update(value As Long)
                value = 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_Update(ref __vb6_point.__vb6_field_X);");
        StringAssert.Contains(source, "__vb6_Update(ref __vb6_point.__vb6_field_Values.Element(");
        Assert.IsFalse(source.Contains("__vb6_byref_temp_", StringComparison.Ordinal));
    }
}
