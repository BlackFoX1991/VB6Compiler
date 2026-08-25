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
    private static readonly object StaticArrayAdapterTarget = new();
    private static int _nextDelegateTypeId;

    /// <summary>
    /// Creates a platform-native callable thunk for a generated VB6 procedure.
    /// </summary>
    public static IntPtr GetFunctionPointer(RuntimeMethodHandle methodHandle, object? target)
    {
        var method = MethodBase.GetMethodFromHandle(methodHandle) as MethodInfo ??
            throw new InvalidOperationException("AddressOf does not refer to a managed method.");
        var parameters = method.GetParameters();

        var arrayElementTypes = parameters
            .Select(parameter => GetArrayElementType(parameter.ParameterType))
            .ToArray();
        var parameterTypes = parameters
            .Select((parameter, index) => arrayElementTypes[index] is null
                ? parameter.ParameterType
                : GetNativeArrayParameterType(parameter.ParameterType))
            .ToArray();
        var variantParameters = parameters
            .Select((parameter, index) => arrayElementTypes[index] is null && IsVariantSlot(parameter))
            .ToArray();
        var returnArrayElementType = GetArrayElementType(method.ReturnType);
        var returnType = returnArrayElementType is null
            ? method.ReturnType
            : typeof(Array);
        var delegateType = GetDelegateType(
            parameterTypes,
            variantParameters,
            arrayElementTypes,
            parameters,
            returnType,
            returnArrayElementType is null && IsVariantSlot(method.ReturnParameter),
            returnArrayElementType,
            method.ReturnParameter);
        var callback = arrayElementTypes.Any(elementType => elementType is not null) ||
                       returnArrayElementType is not null
            ? CreateArrayAdapter(
                method,
                delegateType,
                parameterTypes,
                arrayElementTypes,
                returnArrayElementType,
                target)
            : target is null
                ? method.CreateDelegate(delegateType)
                : method.CreateDelegate(delegateType, target);
        var pointer = Marshal.GetFunctionPointerForDelegate(callback);
        Callbacks[pointer] = callback;
        return pointer;
    }

    private static Type GetDelegateType(
        IReadOnlyList<Type> parameterTypes,
        IReadOnlyList<bool> variantParameters,
        IReadOnlyList<Type?> arrayElementTypes,
        IReadOnlyList<ParameterInfo> sourceParameters,
        Type returnType,
        bool variantReturn,
        Type? returnArrayElementType,
        ParameterInfo returnParameter)
    {
        var key = string.Join(
            ";",
            parameterTypes
                .Select((type, index) =>
                    (type.AssemblyQualifiedName ?? type.FullName ?? type.Name) +
                    (variantParameters[index]
                        ? ":variant"
                        : arrayElementTypes[index] is { } elementType
                            ? ":array:" + GetSafeArrayVariantType(sourceParameters[index], elementType)
                            : ":default"))
                .Append(
                    (returnType.AssemblyQualifiedName ?? returnType.FullName ?? returnType.Name) +
                    (variantReturn
                        ? ":variant"
                        : returnArrayElementType is { } elementType
                            ? ":array:" + GetSafeArrayVariantType(returnParameter, elementType)
                            : ":default")));
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
                ApplyNativeMarshal(
                    parameter,
                    parameterTypes[index],
                    variantParameters[index],
                    arrayElementTypes[index] is { } arrayElement
                        ? GetSafeArrayVariantType(sourceParameters[index], arrayElement)
                        : null);
            }

            ApplyNativeMarshal(
                invoke.DefineParameter(0, ParameterAttributes.None, "return"),
                returnType,
                variantReturn,
                returnArrayElementType is { } returnElementType
                    ? GetSafeArrayVariantType(returnParameter, returnElementType)
                    : null);

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

    private static void ApplyNativeMarshal(
        ParameterBuilder parameter,
        Type type,
        bool variant,
        VarEnum? safeArraySubType)
    {
        var elementType = type.IsByRef ? type.GetElementType()! : type;
        var unmanagedType = safeArraySubType is not null
            ? UnmanagedType.SafeArray
            : variant
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
        if (safeArraySubType is null)
        {
            parameter.SetCustomAttribute(new CustomAttributeBuilder(
                constructor,
                new object[] { unmanagedType.Value }));
            return;
        }

        var safeArrayField = typeof(MarshalAsAttribute).GetField(
            nameof(MarshalAsAttribute.SafeArraySubType),
            BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(
                typeof(MarshalAsAttribute).FullName,
                nameof(MarshalAsAttribute.SafeArraySubType));
        parameter.SetCustomAttribute(new CustomAttributeBuilder(
            constructor,
            new object[] { unmanagedType.Value },
            Array.Empty<PropertyInfo>(),
            Array.Empty<object>(),
            new[] { safeArrayField },
            new object[] { safeArraySubType.Value }));
    }

    private static Type? GetArrayElementType(Type type)
    {
        var elementType = type.IsByRef ? type.GetElementType()! : type;
        return elementType.IsGenericType &&
               elementType.GetGenericTypeDefinition() == typeof(VBArray<>)
            ? elementType.GetGenericArguments()[0]
            : null;
    }

    private static Type GetNativeArrayParameterType(Type sourceType)
    {
        return sourceType.IsByRef
            ? typeof(Array).MakeByRefType()
            : typeof(Array);
    }

    private static VarEnum GetSafeArrayVariantType(ParameterInfo parameter, Type elementType)
    {
        var marshal = parameter.GetCustomAttribute<MarshalAsAttribute>();
        return marshal?.Value == UnmanagedType.SafeArray
            ? marshal.SafeArraySubType
            : GetSafeArrayVariantType(elementType);
    }

    private static VarEnum GetSafeArrayVariantType(Type elementType) =>
        elementType == typeof(byte) ? VarEnum.VT_UI1 :
        elementType == typeof(short) ? VarEnum.VT_I2 :
        elementType == typeof(int) ? VarEnum.VT_I4 :
        elementType == typeof(long) ? VarEnum.VT_I8 :
        elementType == typeof(ushort) ? VarEnum.VT_UI2 :
        elementType == typeof(uint) ? VarEnum.VT_UI4 :
        elementType == typeof(ulong) ? VarEnum.VT_UI8 :
        elementType == typeof(float) ? VarEnum.VT_R4 :
        elementType == typeof(double) ? VarEnum.VT_R8 :
        elementType == typeof(DateTime) ? VarEnum.VT_DATE :
        elementType == typeof(bool) ? VarEnum.VT_BOOL :
        elementType == typeof(string) ? VarEnum.VT_BSTR :
        elementType == typeof(VBCurrency) ? VarEnum.VT_CY :
        elementType == typeof(object) ? VarEnum.VT_VARIANT :
        throw new NotSupportedException(
            $"Callback SAFEARRAY element type '{elementType.FullName}' is not supported.");

    private static Delegate CreateArrayAdapter(
        MethodInfo method,
        Type delegateType,
        IReadOnlyList<Type> nativeParameterTypes,
        IReadOnlyList<Type?> arrayElementTypes,
        Type? returnArrayElementType,
        object? target)
    {
        // Native SAFEARRAY delegates expose CLR arrays; this adapter keeps generated procedures
        // on their bound-preserving VBArray<T> contract and performs ByRef replacement afterward.
        if (!method.IsStatic && target is null)
        {
            throw new InvalidOperationException(
                $"AddressOf instance procedure '{method.Name}' needs an explicit target object.");
        }

        var sourceParameterTypes = method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        var dynamicParameterTypes = new[] { typeof(object) }
            .Concat(nativeParameterTypes)
            .ToArray();
        var dynamicMethod = new DynamicMethod(
            "VB6NativeArrayCallbackAdapter",
            returnArrayElementType is null ? method.ReturnType : typeof(Array),
            dynamicParameterTypes,
            typeof(VBCallbackRegistry).Module,
            skipVisibility: true);
        var generator = dynamicMethod.GetILGenerator();
        var convertedArrays = new LocalBuilder[arrayElementTypes.Count];
        for (var index = 0; index < arrayElementTypes.Count; index++)
        {
            if (arrayElementTypes[index] is null || !sourceParameterTypes[index].IsByRef)
            {
                continue;
            }

            convertedArrays[index] = generator.DeclareLocal(sourceParameterTypes[index].GetElementType()!);
        }

        LocalBuilder? returnValue = null;
        if (method.ReturnType != typeof(void))
        {
            returnValue = generator.DeclareLocal(method.ReturnType);
        }

        if (!method.IsStatic)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Castclass, method.DeclaringType!);
        }

        for (var index = 0; index < sourceParameterTypes.Length; index++)
        {
            var sourceType = sourceParameterTypes[index];
            var arrayElementType = arrayElementTypes[index];
            if (arrayElementType is null)
            {
                generator.Emit(OpCodes.Ldarg, index + 1);
                continue;
            }

            var fromObject = GetArrayConversionMethod(nameof(VBArrayOperations.FromObject), arrayElementType);
            if (sourceType.IsByRef)
            {
                generator.Emit(OpCodes.Ldarg, index + 1);
                generator.Emit(OpCodes.Ldind_Ref);
                generator.Emit(OpCodes.Call, fromObject);
                generator.Emit(OpCodes.Stloc, convertedArrays[index]);
                generator.Emit(OpCodes.Ldloca, convertedArrays[index]);
            }
            else
            {
                generator.Emit(OpCodes.Ldarg, index + 1);
                generator.Emit(OpCodes.Call, fromObject);
            }
        }

        generator.Emit(method.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method);
        if (returnValue is not null)
        {
            generator.Emit(OpCodes.Stloc, returnValue);
        }

        for (var index = 0; index < sourceParameterTypes.Length; index++)
        {
            if (arrayElementTypes[index] is null || !sourceParameterTypes[index].IsByRef)
            {
                continue;
            }

            generator.Emit(OpCodes.Ldarg, index + 1);
            generator.Emit(OpCodes.Ldloc, convertedArrays[index]);
            if (arrayElementTypes[index] == typeof(IntPtr))
            {
                generator.Emit(
                    OpCodes.Ldc_I4,
                    (int)GetSafeArrayVariantType(
                        method.GetParameters()[index],
                        arrayElementTypes[index]!));
                generator.Emit(
                    OpCodes.Call,
                    GetNativeArrayConversionMethod(arrayElementTypes[index]!));
            }
            else
            {
                generator.Emit(
                    OpCodes.Call,
                    GetArrayConversionMethod(nameof(VBArrayOperations.ToClrArray), arrayElementTypes[index]!));
            }
            generator.Emit(OpCodes.Stind_Ref);
        }

        if (returnValue is not null)
        {
            generator.Emit(OpCodes.Ldloc, returnValue);
            if (returnArrayElementType is not null)
            {
                if (returnArrayElementType == typeof(IntPtr))
                {
                    generator.Emit(
                        OpCodes.Ldc_I4,
                        (int)GetSafeArrayVariantType(method.ReturnParameter, returnArrayElementType));
                    generator.Emit(
                        OpCodes.Call,
                        GetNativeArrayConversionMethod(returnArrayElementType));
                }
                else
                {
                    generator.Emit(
                        OpCodes.Call,
                        GetArrayConversionMethod(nameof(VBArrayOperations.ToClrArray), returnArrayElementType));
                }
            }
        }

        generator.Emit(OpCodes.Ret);
        var boundTarget = method.IsStatic ? StaticArrayAdapterTarget : target!;
        return dynamicMethod.CreateDelegate(delegateType, boundTarget);
    }

    private static MethodInfo GetArrayConversionMethod(string name, Type elementType)
    {
        var method = typeof(VBArrayOperations).GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(VBArrayOperations).FullName, name);
        return method.MakeGenericMethod(elementType);
    }

    private static MethodInfo GetNativeArrayConversionMethod(Type elementType)
    {
        var method = typeof(VBArrayOperations).GetMethod(
            nameof(VBArrayOperations.ToNativeSafeArray),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(
                typeof(VBArrayOperations).FullName,
                nameof(VBArrayOperations.ToNativeSafeArray));
        return method.MakeGenericMethod(elementType);
    }
}
