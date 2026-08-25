using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// Keeps managed callback delegates alive after their native function pointer has crossed a
/// Declare/PInvoke boundary. Native code may retain the pointer until process shutdown, so the
/// registry deliberately owns the delegate for the lifetime of the runtime.
/// </summary>
public static class VBCallbackRegistry
{
    private static readonly ConcurrentDictionary<IntPtr, Delegate> Callbacks = new();
    private static readonly Dictionary<string, Type> DelegateTypes = new(StringComparer.Ordinal);
    private static readonly object DelegateTypeLock = new();
    private static readonly AssemblyBuilder CallbackAssembly = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("VB6.Runtime.NativeCallbacks"),
        AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder CallbackModule = CallbackAssembly.DefineDynamicModule(
        "VB6.Runtime.NativeCallbacks");
    private static int _nextDelegateTypeId;

    /// <summary>
    /// Creates a platform-native callable thunk for a generated VB6 procedure.
    /// </summary>
    public static IntPtr GetFunctionPointer(RuntimeMethodHandle methodHandle, object? target)
    {
        var method = MethodBase.GetMethodFromHandle(methodHandle) as MethodInfo ??
            throw new InvalidOperationException("AddressOf does not refer to a managed method.");
        var parameters = method.GetParameters();

        var parameterTypes = parameters.Select(parameter => parameter.ParameterType).ToArray();
        var variantParameters = parameters.Select(IsVariantSlot).ToArray();
        var delegateType = GetDelegateType(
            parameterTypes,
            variantParameters,
            method.ReturnType,
            IsVariantSlot(method.ReturnParameter));
        var callback = target is null
            ? method.CreateDelegate(delegateType)
            : method.CreateDelegate(delegateType, target);
        var pointer = Marshal.GetFunctionPointerForDelegate(callback);
        Callbacks[pointer] = callback;
        return pointer;
    }

    private static Type GetDelegateType(
        IReadOnlyList<Type> parameterTypes,
        IReadOnlyList<bool> variantParameters,
        Type returnType,
        bool variantReturn)
    {
        var key = string.Join(
            ";",
            parameterTypes
                .Select((type, index) =>
                    (type.AssemblyQualifiedName ?? type.FullName ?? type.Name) +
                    (variantParameters[index] ? ":variant" : ":default"))
                .Append(
                    (returnType.AssemblyQualifiedName ?? returnType.FullName ?? returnType.Name) +
                    (variantReturn ? ":variant" : ":default")));
        lock (DelegateTypeLock)
        {
            if (DelegateTypes.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var type = CallbackModule.DefineType(
                "VB6NativeCallback_" + ++_nextDelegateTypeId,
                TypeAttributes.Class |
                TypeAttributes.Public |
                TypeAttributes.Sealed |
                TypeAttributes.AnsiClass |
                TypeAttributes.AutoClass,
                typeof(MulticastDelegate));
            var constructor = type.DefineConstructor(
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new[] { typeof(object), typeof(IntPtr) });
            constructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var invoke = type.DefineMethod(
                "Invoke",
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                returnType,
                parameterTypes.ToArray());
            invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            for (var index = 0; index < parameterTypes.Count; index++)
            {
                var parameter = invoke.DefineParameter(
                    index + 1,
                    ParameterAttributes.None,
                    "arg" + index);
                ApplyNativeMarshal(parameter, parameterTypes[index], variantParameters[index]);
            }

            ApplyNativeMarshal(
                invoke.DefineParameter(0, ParameterAttributes.None, "return"),
                returnType,
                variantReturn);

            var callingConventionAttribute = typeof(UnmanagedFunctionPointerAttribute)
                .GetConstructor(new[] { typeof(CallingConvention) })
                ?? throw new MissingMethodException(typeof(UnmanagedFunctionPointerAttribute).FullName);
            var callingConventionField = typeof(UnmanagedFunctionPointerAttribute)
                .GetField(nameof(UnmanagedFunctionPointerAttribute.CharSet))
                ?? throw new MissingMemberException(
                    typeof(UnmanagedFunctionPointerAttribute).FullName,
                    nameof(UnmanagedFunctionPointerAttribute.CharSet));
            type.SetCustomAttribute(new CustomAttributeBuilder(
                callingConventionAttribute,
                new object[] { CallingConvention.Winapi },
                new[] { callingConventionField },
                new object[] { CharSet.Ansi }));

            var created = type.CreateType()
                ?? throw new InvalidOperationException("Native callback delegate type creation failed.");
            DelegateTypes.Add(key, created);
            return created;
        }
    }

    private static bool IsVariantSlot(ParameterInfo parameter) =>
        parameter.GetCustomAttribute<MarshalAsAttribute>()?.Value == UnmanagedType.Struct;

    private static void ApplyNativeMarshal(ParameterBuilder parameter, Type type, bool variant)
    {
        var elementType = type.IsByRef ? type.GetElementType()! : type;
        var unmanagedType = variant
            ? UnmanagedType.Struct
            : elementType == typeof(bool)
                ? UnmanagedType.Bool
                : elementType == typeof(string)
                    ? UnmanagedType.LPStr
                    : (UnmanagedType?)null;
        if (unmanagedType is null)
        {
            return;
        }

        var constructor = typeof(MarshalAsAttribute).GetConstructor(new[] { typeof(UnmanagedType) })
            ?? throw new MissingMethodException(typeof(MarshalAsAttribute).FullName);
        parameter.SetCustomAttribute(new CustomAttributeBuilder(
            constructor,
            new object[] { unmanagedType.Value }));
    }
}
