using System.Reflection;
using System.Runtime.InteropServices;
using VB6.IR;
using VB6.Runtime;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class AddressOfExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_StoresAddressOfAsLongPtr()
    {
        var output = VB6TestProgram.Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Function Callback(ByVal value As Long) As Long
                Callback = value + 1
            End Function

            Public Sub Main()
                Dim callbackAddress As LongPtr
                callbackAddress = AddressOf Callback
                Debug.Print callbackAddress <> 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void Lower_ConvertsAddressOfToLongForLegacyCallbackDeclare()
    {
        var lowering = VBCompilation.Create("""
            Private Declare Function SetWindowLong Lib "user32" Alias "SetWindowLongA" (ByVal hwnd As Long, ByVal index As Long, ByVal callback As Long) As Long

            Private Function Callback(ByVal hwnd As Long, ByVal message As Long, ByVal wParam As Long, ByVal lParam As Long) As Long
                Callback = 0
            End Function

            Sub Main()
                SetWindowLong 0, 0, AddressOf Callback
            End Sub
            """, "Module1.bas").Lower();

        Assert.IsTrue(lowering.Success, string.Join(Environment.NewLine, lowering.Diagnostics));
        var call = lowering.Program!.Modules
            .SelectMany(module => module.Procedures)
            .Single(procedure => procedure.Name == "Main")
            .Blocks
            .SelectMany(block => block.Instructions)
            .SelectMany(instruction => instruction is IrEvaluateInstruction evaluate
                ? new[] { evaluate.Expression }
                : Array.Empty<IrExpression>())
            .OfType<IrProcedureCallExpression>()
            .Single(callExpression => callExpression.Procedure.Name == "SetWindowLong");

        Assert.AreEqual(TypeSymbol.Long, call.Arguments[2].Expression.Type);
        Assert.IsInstanceOfType<IrAddressOfExpression>(call.Arguments[2].Expression);
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesNativeDeclareCallback()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The native callback test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Declare Function EnumSystemLocalesA Lib "kernel32" Alias "EnumSystemLocalesA" (ByVal callback As LongPtr, ByVal flags As Long) As Long
            Private callbackCount As Long

            Private Function Callback(ByVal localeName As LongPtr) As Long
                callbackCount = callbackCount + 1
                Callback = 1
            End Function

            Sub Main()
                Dim status As Long
                status = EnumSystemLocalesA(AddressOf Callback, 0)
                Debug.Print status <> 0
                Debug.Print callbackCount > 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_MarshalsAnsiStringAndBooleanNativeCallback()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The native ANSI callback test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Declare Function EnumSystemLocalesA Lib "kernel32" Alias "EnumSystemLocalesA" (ByVal callback As LongPtr, ByVal flags As Long) As Long
            Private callbackCount As Long
            Private callbackNameValid As Boolean

            Private Function Callback(ByVal localeName As String) As Boolean
                callbackCount = callbackCount + 1
                callbackNameValid = Len(localeName) > 0
                Callback = True
            End Function

            Sub Main()
                Dim status As Long
                status = EnumSystemLocalesA(AddressOf Callback, 0)
                Debug.Print status <> 0
                Debug.Print callbackCount > 0
                Debug.Print callbackNameValid
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "True", "True" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_MarshalsVariantCallbackSlotsWithoutChangingObjectSlots()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The native Variant callback test requires Windows.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantCallbackTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var assemblyPath = Path.Combine(directory, "VariantCallback.dll");

        try
        {
            var result = VBCompilation.Create("""
                Private Function VariantCallback(ByVal value As Variant) As Variant
                    VariantCallback = value
                End Function

                Private Function ObjectCallback(ByVal value As Object) As Object
                    ObjectCallback = value
                End Function

                Sub Main()
                End Sub
                """, "Module1.bas").EmitManagedApplication(assemblyPath);

            Assert.IsTrue(
                result.Success,
                string.Join(
                    Environment.NewLine,
                    result.Diagnostics.Select(diagnostic => diagnostic.ToString()),
                    result.BackendResult?.Diagnostics.Select(diagnostic => diagnostic.Message) ?? []));

            var assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            var variantMethod = FindGeneratedMethod(assembly, "VariantCallback");
            var objectMethod = FindGeneratedMethod(assembly, "ObjectCallback");
            var variantParameter = variantMethod.GetParameters().Single();
            var objectParameter = objectMethod.GetParameters().Single();

            Assert.AreEqual(typeof(object), variantParameter.ParameterType);
            Assert.AreEqual(
                UnmanagedType.Struct,
                variantParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
            Assert.AreEqual(
                UnmanagedType.Struct,
                variantMethod.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
            Assert.IsNull(objectParameter.GetCustomAttribute<MarshalAsAttribute>());
            Assert.IsNull(objectMethod.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>());

            var pointer = VBCallbackRegistry.GetFunctionPointer(variantMethod.MethodHandle, null);
            var objectPointer = VBCallbackRegistry.GetFunctionPointer(objectMethod.MethodHandle, null);
            var callbackAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly => assembly.GetName().Name == "VB6.Runtime.NativeCallbacks");
            var callbackType = callbackAssembly
                .GetTypes()
                .Single(type =>
                {
                    var invoke = type.GetMethod("Invoke");
                    return invoke is not null &&
                        invoke.GetParameters().Length == 1 &&
                        invoke.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>()?.Value == UnmanagedType.Struct &&
                        invoke.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value == UnmanagedType.Struct;
                });
            var callback = Marshal.GetDelegateForFunctionPointer(pointer, callbackType);
            Assert.AreEqual(17, callback.DynamicInvoke(17));

            var objectCallbackType = callbackAssembly
                .GetTypes()
                .Single(type =>
                {
                    var invoke = type.GetMethod("Invoke");
                    return invoke is not null &&
                        invoke.GetParameters().Length == 1 &&
                        invoke.GetParameters()[0].GetCustomAttribute<MarshalAsAttribute>() is null &&
                        invoke.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>() is null;
                });
            Assert.AreNotEqual(callbackType, objectCallbackType);
            var objectCallback = Marshal.GetDelegateForFunctionPointer(objectPointer, objectCallbackType);
            Assert.AreEqual(19, objectCallback.DynamicInvoke(19));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedApplication_InvokesNativeDeclareByRefUdtCallback()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The native ByRef callback test requires Windows.");
            return;
        }

        var output = VB6TestProgram.Run("""
            Private Type RECT
                Left As Long
                Top As Long
                Right As Long
                Bottom As Long
            End Type

            Private Declare Function EnumDisplayMonitors Lib "user32" (ByVal hdc As LongPtr, ByVal clipRect As LongPtr, ByVal callback As LongPtr, ByVal data As LongPtr) As Long
            Private callbackCount As Long
            Private callbackShapeValid As Boolean

            Private Function Callback(ByVal monitor As LongPtr, ByVal hdc As LongPtr, ByRef monitorRect As RECT, ByVal data As LongPtr) As Long
                callbackCount = callbackCount + 1
                callbackShapeValid = monitorRect.Right > monitorRect.Left And monitorRect.Bottom > monitorRect.Top
                Callback = 1
            End Function

            Sub Main()
                Dim status As Long
                status = EnumDisplayMonitors(0, 0, AddressOf Callback, 0)
                Debug.Print status <> 0
                Debug.Print callbackCount > 0
                Debug.Print callbackShapeValid
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True" }, VB6TestProgram.SplitLines(output), output);
    }

    private static MethodInfo FindGeneratedMethod(Assembly assembly, string name)
    {
        return assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .Single(method => method.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

}
