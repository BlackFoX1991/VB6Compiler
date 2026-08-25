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

    private static int DateArrayCallback(ref VBArray<DateTime> values)
    {
        values = new VBArray<DateTime>(new VBArrayBound(4, 5));
        values[4] = new DateTime(2020, 2, 4);
        values[5] = new DateTime(2020, 2, 5);
        return 1;
    }
}
