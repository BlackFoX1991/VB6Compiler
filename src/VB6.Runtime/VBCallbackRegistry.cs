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
        if (parameters.Any(parameter => parameter.ParameterType.IsByRef))
        {
            throw new NotSupportedException(
                $"Callback '{method.Name}' contains ByRef parameters, which need an explicit callback ABI.");
        }

        var delegateSignature = parameters
            .Select(parameter => parameter.ParameterType)
            .Append(method.ReturnType)
            .ToArray();
        var delegateType = GetDelegateType(delegateSignature);
        var callback = target is null
            ? method.CreateDelegate(delegateType)
            : method.CreateDelegate(delegateType, target);
        var pointer = Marshal.GetFunctionPointerForDelegate(callback);
        Callbacks[pointer] = callback;
        return pointer;
    }

    private static Type GetDelegateType(IReadOnlyList<Type> signature)
    {
        var key = string.Join(
            ";",
            signature.Select(type => type.AssemblyQualifiedName ?? type.FullName ?? type.Name));
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
                signature[^1],
                signature.Take(signature.Count - 1).ToArray());
            invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var callingConventionAttribute = typeof(UnmanagedFunctionPointerAttribute)
                .GetConstructor(new[] { typeof(CallingConvention) })
                ?? throw new MissingMethodException(typeof(UnmanagedFunctionPointerAttribute).FullName);
            type.SetCustomAttribute(new CustomAttributeBuilder(
                callingConventionAttribute,
                new object[] { CallingConvention.Winapi }));

            var created = type.CreateType()
                ?? throw new InvalidOperationException("Native callback delegate type creation failed.");
            DelegateTypes.Add(key, created);
            return created;
        }
    }
}
