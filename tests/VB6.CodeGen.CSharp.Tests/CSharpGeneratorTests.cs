using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class CSharpGeneratorTests
{
    [TestMethod]
    public void Generate_EmitsAcceptanceProgram()
    {
        var analysis = VBCompilation.Create("""
            Option Explicit

            Sub Main()
                Dim x As Integer
                x = 10

                If x > 5 Then
                    Debug.Print x
                End If
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "public static void Main()");
        StringAssert.Contains(source, "short __vb6_x = 0;");
        StringAssert.Contains(source, "__vb6_x = VBConversions.CInt(10L);");
        StringAssert.Contains(source, "if (VBOperators.Greater(__vb6_x, VBConversions.CInt(5L)))");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_x);");
    }

    [TestMethod]
    public void Generate_EmitsVbConversionCalls()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = "10"
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_x = VBConversions.CInt(\"10\");");
    }

    [TestMethod]
    public void Generate_EmitsImplicitVariantDeclarators()
    {
        var analysis = VBCompilation.Create("""
            Dim moduleValue

            Sub Main()
                Dim localValue
                Dim emptyValue
                localValue = 10
                emptyValue = Empty
                moduleValue = "ok"
                Debug.Print localValue
                Debug.Print IsEmpty(emptyValue)
                Debug.Print IsNull(Null)
                Debug.Print IsError(CVErr(5))
                Debug.Print Probe()
                Debug.Print IsNumeric(localValue)
                Debug.Print VarType(moduleValue)
            End Sub

            Function Probe(Optional ByVal value) As Boolean
                Probe = IsMissing(value)
            End Function
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private static VBVariant __vb6_moduleValue = VBVariant.Empty;");
        StringAssert.Contains(source, "VBVariant __vb6_localValue = VBVariant.Empty;");
        StringAssert.Contains(source, "__vb6_localValue = VBVariant.From(VBConversions.CInt(10L));");
        StringAssert.Contains(source, "__vb6_emptyValue = VBVariant.Empty;");
        StringAssert.Contains(source, "__vb6_moduleValue = VBVariant.From(\"ok\");");
        StringAssert.Contains(source, "VBVariantFunctions.IsEmpty(__vb6_emptyValue)");
        StringAssert.Contains(source, "VBVariantFunctions.IsNull(VBVariant.Null)");
        StringAssert.Contains(source, "VBVariantFunctions.IsError(VBVariantFunctions.CVErr(VBConversions.CInt(5L)))");
        StringAssert.Contains(source, "VBVariantFunctions.IsMissing(__vb6_arg_value)");
        StringAssert.Contains(source, "__vb6_Probe(VBVariant.Missing)");
        StringAssert.Contains(source, "VBVariantFunctions.IsNumeric(__vb6_localValue)");
        StringAssert.Contains(source, "VBVariantFunctions.VarType(__vb6_moduleValue)");
    }

    [TestMethod]
    public void Generate_EmitsVariantBinaryOperators()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value
                value = 2
                Debug.Print value + 3
                Debug.Print value & "x"
                Debug.Print value = 2
                Debug.Print value And 3
                Debug.Print value Or 1
                Debug.Print value Imp 0
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBVariantOperators.Add(__vb6_value, VBConversions.CInt(3L))");
        StringAssert.Contains(source, "VBVariantOperators.Concat(__vb6_value, \"x\")");
        StringAssert.Contains(source, "VBVariantOperators.Equal(__vb6_value, VBConversions.CInt(2L))");
        StringAssert.Contains(source, "VBVariantOperators.And(__vb6_value, VBConversions.CInt(3L))");
        StringAssert.Contains(source, "VBVariantOperators.Or(__vb6_value, VBConversions.CInt(1L))");
        StringAssert.Contains(source, "VBVariantOperators.Imp(__vb6_value, VBConversions.CInt(0L))");
    }

    [TestMethod]
    public void Generate_EmitsProcedureCalls()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Helper
            End Sub

            Sub Helper()
                Debug.Print 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_Helper();");
        StringAssert.Contains(source, "private static void __vb6_Helper()");
    }

    [TestMethod]
    public void Generate_EmitsOmittedOptionalByValDefaults()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Configure
                AcceptMissing
            End Sub

            Sub Configure(Optional ByVal retries As Long = 3)
            End Sub

            Sub AcceptMissing(Optional ByVal value)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_Configure(VBConversions.CLng(VBConversions.CInt(3L)));");
        StringAssert.Contains(source, "__vb6_AcceptMissing(VBVariant.Missing);");
    }

    [TestMethod]
    public void Generate_EmitsOmittedOptionalByRefDefaultAsTemporary()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Configure
            End Sub

            Sub Configure(Optional value)
                value = 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBVariant __vb6_byref_temp_0 = VBVariant.Missing;");
        StringAssert.Contains(source, "__vb6_Configure(ref __vb6_byref_temp_0);");
    }

    [TestMethod]
    public void Generate_EmitsByRefAndByValParameters()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 5
                Call Update(x)
                Call Observe(x)
            End Sub

            Sub Update(value As Integer)
                value = 10
            End Sub

            Sub Observe(ByVal value As Integer)
                value = 20
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private static void __vb6_Update(ref short __vb6_arg_value)");
        StringAssert.Contains(source, "private static void __vb6_Observe(short __vb6_arg_value)");
        StringAssert.Contains(source, "__vb6_Update(ref __vb6_x);");
        StringAssert.Contains(source, "__vb6_Observe(__vb6_x);");
        StringAssert.Contains(source, "__vb6_arg_value = VBConversions.CInt(10L);");
    }

    [TestMethod]
    public void Generate_EmitsParenthesizedByRefArgumentAsTemporary()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 5
                Call Update((x))
                Debug.Print x
            End Sub

            Sub Update(value As Integer)
                value = 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "short __vb6_byref_temp_0 = __vb6_x;");
        StringAssert.Contains(source, "__vb6_Update(ref __vb6_byref_temp_0);");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_x);");
    }

    [TestMethod]
    public void Generate_EmitsCallSiteByValByRefArgumentAsTemporary()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 5
                Call Update(ByVal x)
                Debug.Print x
            End Sub

            Sub Update(value As Integer)
                value = 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "short __vb6_byref_temp_0 = __vb6_x;");
        StringAssert.Contains(source, "__vb6_Update(ref __vb6_byref_temp_0);");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_x);");
    }

    [TestMethod]
    public void Generate_EmitsByRefCopyBackTemporaryForScalarTypeMismatch()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Long
                x = 5
                Call Update(x)
                Debug.Print x
            End Sub

            Sub Update(value As Integer)
                value = 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "short __vb6_byref_temp_0 = VBConversions.CInt(__vb6_x);");
        StringAssert.Contains(source, "__vb6_Update(ref __vb6_byref_temp_0);");
        StringAssert.Contains(source, "__vb6_x = VBConversions.CLng(__vb6_byref_temp_0);");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_x);");
    }

    [TestMethod]
    public void Generate_EmitsArrayElementByRefArgument()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim values(1 To 1) As Long
                values(1) = 5
                Call Update(values(1))
            End Sub

            Sub Update(value As Long)
                value = 10
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_Update(ref __vb6_values.Element(");
        Assert.IsFalse(source.Contains("__vb6_byref_temp_", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_EmitsByRefTemporaryForFunctionCallExpression()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                Dim result As Integer
                x = 5
                result = Mutate((x))
                Debug.Print x + result
            End Sub

            Function Mutate(value As Integer) As Integer
                value = 10
                Mutate = value
            End Function
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "((System.Func<short>)(() => { short __vb6_byref_temp_0 = __vb6_x; short __vb6_byref_result_1 = __vb6_Mutate(ref __vb6_byref_temp_0); return __vb6_byref_result_1; }))()");
        StringAssert.Contains(source, "VBDebug.Print(VBOperators.AddInteger(__vb6_x, __vb6_result));");
    }

    [TestMethod]
    public void Generate_EmitsByRefCopyBackTemporaryForFunctionCallExpression()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Long
                Dim result As Integer
                x = 5
                result = Mutate(x)
                Debug.Print x + result
            End Sub

            Function Mutate(value As Integer) As Integer
                value = 10
                Mutate = value
            End Function
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "short __vb6_byref_temp_0 = VBConversions.CInt(__vb6_x);");
        StringAssert.Contains(source, "short __vb6_byref_result_1 = __vb6_Mutate(ref __vb6_byref_temp_0);");
        StringAssert.Contains(source, "__vb6_x = VBConversions.CLng(__vb6_byref_temp_0);");
        StringAssert.Contains(source, "return __vb6_byref_result_1;");
    }

    [TestMethod]
    public void Generate_EmitsOmittedOptionalByRefTemporaryForFunctionCallExpression()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim result As Long
                result = Configure()
                Debug.Print result
            End Sub

            Function Configure(Optional value) As Long
                Configure = 7
            End Function
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "((System.Func<int>)(() => { VBVariant __vb6_byref_temp_0 = VBVariant.Missing; int __vb6_byref_result_1 = __vb6_Configure(ref __vb6_byref_temp_0); return __vb6_byref_result_1; }))()");
    }

    [TestMethod]
    public void Generate_EmitsStaticLocalsAsProcedureScopedFields()
    {
        var analysis = VBCompilation.Create("""
            Function NextValue() As Long
                Static count As Long
                count = count + 1
                NextValue = count
            End Function
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private static int __vb6_static_NextValue_count = 0;");
        StringAssert.Contains(source, "__vb6_static_NextValue_count = VBOperators.AddLong(__vb6_static_NextValue_count, VBConversions.CLng(VBConversions.CInt(1L)));");
        StringAssert.Contains(source, "__vb6_return = __vb6_static_NextValue_count;");
        Assert.IsFalse(source.Contains("int __vb6_count = 0;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Generate_EmitsFunctionReturnSlotAndCallExpression()
    {
        var analysis = VBCompilation.Create("""
            Function Add(ByVal left As Integer, ByVal right As Integer) As Integer
                Add = left + right
            End Function

            Sub Main()
                Dim result As Integer
                result = Add(5, 7)
                Debug.Print result
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private static short __vb6_Add(short __vb6_arg_left, short __vb6_arg_right)");
        StringAssert.Contains(source, "short __vb6_return = 0;");
        StringAssert.Contains(source, "__vb6_return = VBOperators.AddInteger(__vb6_arg_left, __vb6_arg_right);");
        StringAssert.Contains(source, "return __vb6_return;");
        StringAssert.Contains(source, "__vb6_result = __vb6_Add(VBConversions.CInt(5L), VBConversions.CInt(7L));");
    }

    [TestMethod]
    public void Generate_EmitsFixedArrayCreationAndElementAccess()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim values(1 To 3) As Long
                values(1) = 10
                Debug.Print values(1)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBArray<int> __vb6_values = new VBArray<int>(new VBArrayBound(");
        StringAssert.Contains(source, "__vb6_values[");
        StringAssert.Contains(source, "] = VBConversions.CLng(");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_values[");
    }

    [TestMethod]
    public void Generate_EmitsReDimEraseAndArrayBounds()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim values() As Long
                ReDim values(2 To 4)
                Debug.Print LBound(values) + UBound(values)
                Erase values
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBArray<int> __vb6_values = default!;");
        StringAssert.Contains(source, "__vb6_values = new VBArray<int>(new VBArrayBound(");
        StringAssert.Contains(source, "__vb6_values.LBound(");
        StringAssert.Contains(source, "__vb6_values.UBound(");
        StringAssert.Contains(source, "__vb6_values = default!;");
    }

    [TestMethod]
    public void Generate_EmitsReDimPreserveResizeCall()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim values() As Long
                ReDim values(1 To 2)
                ReDim Preserve values(1 To 3)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_values = __vb6_values.ResizePreserve(new VBArrayBound(");
    }

    [TestMethod]
    public void Generate_EmitsForEachOverVbArrayValues()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim values(1 To 2) As Long
                Dim value As Long
                For Each value In values
                    Debug.Print value
                Next value
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "foreach (var __vb6_for_each_item_");
        StringAssert.Contains(source, " in __vb6_values.Values())");
        StringAssert.Contains(source, "__vb6_value = VBConversions.CLng(__vb6_for_each_item_");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_value);");
    }

    [TestMethod]
    public void Generate_EmitsForWhileDoAndExitTargets()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim i As Integer
                i = 0

                For i = 3 To 1 Step -1
                    Do
                        Exit For
                    Loop
                Next i

                While i < 2
                    i = i + 1
                Wend

                Do
                    i = i + 1
                Loop Until i = 3
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "short __vb6_for_limit_");
        StringAssert.Contains(source, "short __vb6_for_step_");
        StringAssert.Contains(source, "VBOperators.LessOrEqual(__vb6_i");
        StringAssert.Contains(source, "VBOperators.GreaterOrEqual(__vb6_i");
        StringAssert.Contains(source, "while (VBOperators.Less(__vb6_i, VBConversions.CInt(2L)))");
        StringAssert.Contains(source, "do");
        StringAssert.Contains(source, "while (!(VBOperators.Equal(__vb6_i, VBConversions.CInt(3L))));");
        StringAssert.Contains(source, "goto __vb6_loop_exit_");
    }

    [TestMethod]
    public void Emit_ProducesManagedAssembly()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 10
                Debug.Print x
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);
        using var peStream = new MemoryStream();

        var emitResult = new CSharpAssemblyEmitter().Emit(source, "GeneratedProgram", peStream);

        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
        Assert.IsTrue(peStream.Length > 0);
    }
}
