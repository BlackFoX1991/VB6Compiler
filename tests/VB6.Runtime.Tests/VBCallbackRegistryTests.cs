using System.Reflection;
using System.Runtime.InteropServices;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBCallbackRegistryTests
{
    [TestMethod]
    public void AddressOfAdapter_UsesDateSafeArrayAndWritesBackReplacement()
    {
        var method = typeof(VBCallbackRegistryTests).GetMethod(
            nameof(DateArrayCallback),
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var pointer = VBCallbackRegistry.GetFunctionPointer(method.MethodHandle, null);
        var callbackAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "VB6.Runtime.NativeCallbacks");
        var callbackType = callbackAssembly
            .GetTypes()
            .Single(type =>
            {
                var invoke = type.GetMethod("Invoke");
                var parameter = invoke?.GetParameters().SingleOrDefault();
                var marshal = parameter?.GetCustomAttribute<MarshalAsAttribute>();
                return invoke?.ReturnType == typeof(int) &&
                    parameter?.ParameterType == typeof(Array).MakeByRefType() &&
                    marshal?.Value == UnmanagedType.SafeArray &&
                    marshal.SafeArraySubType == VarEnum.VT_DATE;
            });

        var callback = Marshal.GetDelegateForFunctionPointer(pointer, callbackType);
        Array values = Array.CreateInstance(typeof(DateTime), new[] { 2 }, new[] { -1 });
        values.SetValue(new DateTime(2020, 1, 2), -1);
        values.SetValue(new DateTime(2020, 1, 3), 0);
        var arguments = new object?[] { values };

        Assert.AreEqual(1, callback.DynamicInvoke(arguments));

        values = (Array)arguments[0]!;
        Assert.AreEqual(typeof(DateTime), values.GetType().GetElementType());
        Assert.AreEqual(4, values.GetLowerBound(0));
        Assert.AreEqual(5, values.GetUpperBound(0));
        Assert.AreEqual(new DateTime(2020, 2, 4), values.GetValue(4));
        Assert.AreEqual(new DateTime(2020, 2, 5), values.GetValue(5));
    }

    [TestMethod]
    public void AddressOfAdapter_KeepsVariantStateMarkersAcrossASafeArray()
    {
        var method = typeof(VBCallbackRegistryTests).GetMethod(
            nameof(VariantMarkerCallback),
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var pointer = VBCallbackRegistry.GetFunctionPointer(method.MethodHandle, null);
        var callbackAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "VB6.Runtime.NativeCallbacks");
        var callbackType = callbackAssembly
            .GetTypes()
            .Single(type =>
            {
                var invoke = type.GetMethod("Invoke");
                var parameter = invoke?.GetParameters().SingleOrDefault();
                var marshal = parameter?.GetCustomAttribute<MarshalAsAttribute>();
                return invoke?.ReturnType == typeof(int) &&
                    parameter?.ParameterType == typeof(Array).MakeByRefType() &&
                    marshal?.Value == UnmanagedType.SafeArray &&
                    marshal.SafeArraySubType == VarEnum.VT_VARIANT;
            });

        // Empty, Null und Nothing sind in VB6 drei verschiedene Zustaende eines Variant, und ein
        // Weg ueber eine SAFEARRAY darf sie nicht zu einem einzigen "nichts" verschmelzen.
        var callback = Marshal.GetDelegateForFunctionPointer(pointer, callbackType);
        Array values = Array.CreateInstance(typeof(object), new[] { 1 }, new[] { 0 });
        var arguments = new object?[] { values };

        Assert.AreEqual(1, callback.DynamicInvoke(arguments));

        values = (Array)arguments[0]!;
        Assert.AreEqual(0, values.GetLowerBound(0));
        Assert.AreEqual(4, values.GetUpperBound(0));

        Assert.AreEqual(0, VBVariants.VarType(values.GetValue(0)), "Empty bleibt vbEmpty.");
        Assert.AreEqual(1, VBVariants.VarType(values.GetValue(1)), "Null bleibt vbNull.");
        Assert.AreEqual(9, VBVariants.VarType(values.GetValue(2)), "Nothing bleibt vbObject.");
        Assert.AreEqual(8, VBVariants.VarType(values.GetValue(3)), "Eine Zeichenkette bleibt vbString.");
        Assert.AreEqual(3, VBVariants.VarType(values.GetValue(4)), "Eine Long bleibt vbLong.");
        Assert.AreEqual("marker", values.GetValue(3));
        Assert.AreEqual(42, values.GetValue(4));
    }

    private static int VariantMarkerCallback(ref VBArray<object> values)
    {
        values = new VBArray<object>(new VBArrayBound(0, 4));
        values[0] = VBVariants.EmptyValue()!;
        values[1] = VBVariants.NullValue();
        values[2] = VBVariants.NothingValue();
        values[3] = "marker";
        values[4] = 42;
        return 1;
    }

    private static int DateArrayCallback(ref VBArray<DateTime> values)
    {
        values = new VBArray<DateTime>(new VBArrayBound(4, 5));
        values[4] = new DateTime(2020, 2, 4);
        values[5] = new DateTime(2020, 2, 5);
        return 1;
    }
}
