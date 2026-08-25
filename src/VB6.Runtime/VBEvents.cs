using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// Managed event storage for emitted VB6 class instances. The compiler keeps event identity in IR;
/// this hub supplies a host-facing subscription contract without baking .NET delegate signatures
/// into generated class metadata.
/// </summary>
public static class VBEvents
{
    private static readonly object Sync = new();
    private static readonly Dictionary<object, Dictionary<string, List<Action<object?[]>>>> Sinks =
        new(ReferenceEqualityComparer.Instance);
    private static readonly List<MethodSubscription> MethodSubscriptions = new();
    private static readonly Dictionary<string, Type> ComEventDelegateTypes = new(StringComparer.Ordinal);
    private static readonly object ComEventDelegateTypeSync = new();
    private static readonly AssemblyBuilder ComEventDelegateAssembly =
        AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("VB6.Runtime.ComEventDelegates"),
            AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder ComEventDelegateModule =
        ComEventDelegateAssembly.DefineDynamicModule("VB6.Runtime.ComEventDelegates");
    private static int _nextComEventDelegateTypeId;

    public static void Subscribe(
        object source,
        string eventName,
        Action<object?[]> handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (Sync)
        {
            if (!Sinks.TryGetValue(source, out var events))
            {
                events = new Dictionary<string, List<Action<object?[]>>>(StringComparer.OrdinalIgnoreCase);
                Sinks.Add(source, events);
            }

            if (!events.TryGetValue(eventName, out var handlers))
            {
                handlers = new List<Action<object?[]>>();
                events.Add(eventName, handlers);
            }

            handlers.Add(handler);
        }
    }

    public static void Unsubscribe(
        object source,
        string eventName,
        Action<object?[]> handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (Sync)
        {
            if (!Sinks.TryGetValue(source, out var events) ||
                !events.TryGetValue(eventName, out var handlers))
            {
                return;
            }

            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                events.Remove(eventName);
            }

            if (events.Count == 0)
            {
                Sinks.Remove(source);
            }
        }
    }

    public static void SubscribeMethod(
        object? source,
        string eventName,
        object target,
        string methodName)
    {
        SubscribeMethod(source, eventName, target, methodName, null, int.MinValue);
    }

    /// <summary>
    /// Removes a generated-method subscription. A null source removes matching subscriptions
    /// from every source, which is the explicit form of the VB6 event-variable reset operation.
    /// </summary>
    public static void UnsubscribeMethod(
        object? source,
        string eventName,
        object target,
        string methodName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        lock (Sync)
        {
            foreach (var existing in MethodSubscriptions
                         .Where(subscription =>
                             (source is null || ReferenceEquals(subscription.Source, source)) &&
                             ReferenceEquals(subscription.Target, target) &&
                             string.Equals(subscription.EventName, eventName, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(subscription.MethodName, methodName, StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                RemoveSubscriptionLocked(existing);
                MethodSubscriptions.Remove(existing);
            }
        }
    }

    /// <summary>
    /// Removes every generated-method subscription that references the supplied source or target.
    /// Hosts use this when a form/control graph is torn down so COM connection points do not keep
    /// native wrappers and generated event sinks alive after VB6 object termination.
    /// </summary>
    public static void UnsubscribeObject(object sourceOrTarget)
    {
        ArgumentNullException.ThrowIfNull(sourceOrTarget);

        lock (Sync)
        {
            foreach (var existing in MethodSubscriptions
                         .Where(subscription =>
                             ReferenceEquals(subscription.Source, sourceOrTarget) ||
                             ReferenceEquals(subscription.Target, sourceOrTarget))
                         .ToArray())
            {
                RemoveSubscriptionLocked(existing);
                MethodSubscriptions.Remove(existing);
            }
        }
    }

    /// <summary>
    /// Retries COM subscriptions that were created before an ActiveX wrapper had a live COM
    /// object. This is needed for <c>WithEvents</c> assignments made from <c>Form_Load</c>.
    /// </summary>
    public static void RetryComSubscriptions(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (Sync)
        {
            foreach (var existing in MethodSubscriptions
                         .Where(subscription =>
                             ReferenceEquals(subscription.Source, source) &&
                             subscription.Handler is not null &&
                             subscription.Source is IVBComObjectProvider)
                         .ToArray())
            {
                if (existing.Handler is not { } fallbackHandler)
                {
                    continue;
                }

                var method = FindHandler(existing.Target, existing.MethodName);
                if (method is null ||
                    !TrySubscribeComEvent(
                        existing.Source,
                        existing.EventName,
                        existing.ComInterfaceId?.ToString("D"),
                        existing.ComDispId,
                        existing.Target,
                        method,
                        out var interfaceId,
                        out var dispId,
                        out var @delegate))
                {
                    continue;
                }

                RemoveHandlerLocked(existing.Source, existing.EventName, fallbackHandler);
                MethodSubscriptions.Remove(existing);
                MethodSubscriptions.Add(new MethodSubscription(
                    existing.Source,
                    existing.EventName,
                    existing.Target,
                    existing.MethodName,
                    handler: null,
                    host: null,
                    eventInfo: null,
                    @delegate,
                    interfaceId,
                    dispId));
            }
        }
    }

    public static void SubscribeMethod(
        object? source,
        string eventName,
        object target,
        string methodName,
        string? comInterfaceId,
        int comDispId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        var method = FindHandler(target, methodName)
                     ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        var handler = new Action<object?[]>(arguments => method.Invoke(target, arguments));
        var hasImportedIdentity = Guid.TryParse(comInterfaceId, out var importedInterfaceGuid) &&
            comDispId != int.MinValue;
        var importedDispId = hasImportedIdentity ? comDispId : (int?)null;

        lock (Sync)
        {
            foreach (var existing in MethodSubscriptions
                         .Where(subscription =>
                             ReferenceEquals(subscription.Target, target) &&
                             string.Equals(subscription.EventName, eventName, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(subscription.MethodName, methodName, StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                RemoveSubscriptionLocked(existing);
                MethodSubscriptions.Remove(existing);
            }

            if (source is null)
            {
                return;
            }

            var host = VBInteraction.Host;
            if (source is not IVBComObjectProvider &&
                host is not null &&
                host.TrySubscribeEvent(source, eventName, target, methodName))
            {
                MethodSubscriptions.Add(new MethodSubscription(
                    source,
                    eventName,
                    target,
                    methodName,
                    handler: null,
                    host: host));
                return;
            }

            if (TrySubscribeClrEvent(source, eventName, target, method, out var eventInfo, out var @delegate))
            {
                MethodSubscriptions.Add(new MethodSubscription(
                    source,
                    eventName,
                    target,
                    methodName,
                    handler: null,
                    host: null,
                    eventInfo,
                    @delegate));
                return;
            }

            if (TrySubscribeComEvent(
                    source,
                    eventName,
                    comInterfaceId,
                    comDispId == int.MinValue ? null : comDispId,
                    target,
                    method,
                    out var comInterfaceGuid,
                    out var comEventDispId,
                    out @delegate))
            {
                MethodSubscriptions.Add(new MethodSubscription(
                    source,
                    eventName,
                    target,
                    methodName,
                    handler: null,
                    host: null,
                    eventInfo: null,
                    @delegate,
                    comInterfaceGuid,
                    comEventDispId));
                return;
            }

            AddHandlerLocked(source, eventName, handler);
            MethodSubscriptions.Add(new MethodSubscription(
                source,
                eventName,
                target,
                methodName,
                handler,
                host: null,
                eventInfo: null,
                @delegate: null,
                comInterfaceId: hasImportedIdentity ? importedInterfaceGuid : null,
                comDispId: importedDispId));
        }
    }

    /// <summary>
    /// Connects a native COM event without routing through the host's managed CLR-event adapter.
    /// This is used for conventional designer handlers whose source is an ActiveX control shell.
    /// </summary>
    public static bool TrySubscribeComMethod(
        object? source,
        string eventName,
        object target,
        string methodName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        var method = FindHandler(target, methodName);
        if (method is null)
        {
            return false;
        }

        lock (Sync)
        {
            foreach (var existing in MethodSubscriptions
                         .Where(subscription =>
                             ReferenceEquals(subscription.Target, target) &&
                             string.Equals(subscription.EventName, eventName, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(subscription.MethodName, methodName, StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                RemoveSubscriptionLocked(existing);
                MethodSubscriptions.Remove(existing);
            }

            if (source is null ||
                !TrySubscribeComEvent(
                    source,
                    eventName,
                    comInterfaceId: null,
                    comDispId: null,
                    target,
                    method,
                    out var comInterfaceId,
                    out var comDispId,
                    out var @delegate))
            {
                return false;
            }

            MethodSubscriptions.Add(new MethodSubscription(
                source,
                eventName,
                target,
                methodName,
                handler: null,
                host: null,
                eventInfo: null,
                @delegate,
                comInterfaceId,
                comDispId));
            return true;
        }
    }

    public static void Raise(object source, string eventName, object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(arguments);

        Action<object?[]>[] handlers;
        lock (Sync)
        {
            handlers = Sinks.TryGetValue(source, out var events) &&
                       events.TryGetValue(eventName, out var registered)
                ? registered.ToArray()
                : Array.Empty<Action<object?[]>>();
        }

        foreach (var handler in handlers)
        {
            handler(arguments);
        }
    }

    private static string Mangle(string name) =>
        new(name.Select(character =>
            char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());

    private static void AddHandlerLocked(
        object source,
        string eventName,
        Action<object?[]> handler)
    {
        if (!Sinks.TryGetValue(source, out var events))
        {
            events = new Dictionary<string, List<Action<object?[]>>>(StringComparer.OrdinalIgnoreCase);
            Sinks.Add(source, events);
        }

        if (!events.TryGetValue(eventName, out var handlers))
        {
            handlers = new List<Action<object?[]>>();
            events.Add(eventName, handlers);
        }

        handlers.Add(handler);
    }

    private static void RemoveHandlerLocked(
        object source,
        string eventName,
        Action<object?[]> handler)
    {
        if (!Sinks.TryGetValue(source, out var events) ||
            !events.TryGetValue(eventName, out var handlers))
        {
            return;
        }

        handlers.Remove(handler);
        if (handlers.Count == 0)
        {
            events.Remove(eventName);
        }

        if (events.Count == 0)
        {
            Sinks.Remove(source);
        }
    }

    private static void RemoveSubscriptionLocked(MethodSubscription subscription)
    {
        if (subscription.Host is not null)
        {
            subscription.Host.UnsubscribeEvent(
                subscription.Source,
                subscription.EventName,
                subscription.Target,
                subscription.MethodName);
        }
        else if (subscription.EventInfo is not null && subscription.Delegate is not null)
        {
            subscription.EventInfo.RemoveEventHandler(subscription.Source, subscription.Delegate);
        }
        else if (subscription.ComInterfaceId is Guid interfaceId &&
                 subscription.ComDispId is int dispId &&
                 subscription.Delegate is not null)
        {
            var comSource = GetComObject(subscription.Source);
            if (OperatingSystem.IsWindows())
            {
                if (comSource is not null)
                {
                    ComEventsHelper.Remove(
                        comSource,
                        interfaceId,
                        dispId,
                        subscription.Delegate);
                }
            }
        }
        else if (subscription.Handler is not null)
        {
            RemoveHandlerLocked(subscription.Source, subscription.EventName, subscription.Handler);
        }
    }

    private static bool TrySubscribeClrEvent(
        object source,
        string eventName,
        object target,
        MethodInfo method,
        out EventInfo? eventInfo,
        out Delegate? @delegate)
    {
        eventInfo = source.GetType()
            .GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, eventName, StringComparison.OrdinalIgnoreCase));
        @delegate = null;
        if (source is IVBComObjectProvider)
        {
            // AxHost exposes inherited WinForms events as well as the wrapped OCX's COM
            // connection points. A VB6 event name must use the COM identity for such a source;
            // otherwise a matching wrapper event can hide ByRef and Automation parameters.
            eventInfo = null;
            return false;
        }

        if (eventInfo?.EventHandlerType is null || eventInfo.GetAddMethod(true) is null)
        {
            eventInfo = null;
            return false;
        }

        try
        {
            var callback = new EventCallback(target, method);
            @delegate = CreateEventDelegate(eventInfo.EventHandlerType, callback);
            eventInfo.AddEventHandler(source, @delegate);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            NotSupportedException or
            TargetException or
            TargetInvocationException or
            COMException)
        {
            eventInfo = null;
            @delegate = null;
            return false;
        }
    }

    private static MethodInfo? FindHandler(object target, string methodName)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = target.GetType();
        return type.GetMethods(flags)
                   .FirstOrDefault(candidate =>
                       string.Equals(candidate.Name, methodName, StringComparison.OrdinalIgnoreCase)) ??
               type.GetMethods(flags)
                   .FirstOrDefault(candidate =>
                       string.Equals(
                           candidate.Name,
                           "__vb6_" + Mangle(methodName),
                           StringComparison.OrdinalIgnoreCase));
    }

    private static bool TrySubscribeComEvent(
        object source,
        string eventName,
        string? comInterfaceId,
        int? comDispId,
        object target,
        MethodInfo method,
        out Guid? interfaceId,
        out int? dispId,
        out Delegate? @delegate)
    {
        interfaceId = null;
        dispId = null;
        @delegate = null;
        var comSource = GetComObject(source);
        if (!OperatingSystem.IsWindows() ||
            comSource is null ||
            !Marshal.IsComObject(comSource))
        {
            return false;
        }

        var parsedInterfaceId = Guid.Empty;
        var parsedDispId = 0;
        var hasImportedIdentity = Guid.TryParse(comInterfaceId, out parsedInterfaceId) &&
            comDispId.HasValue;
        if (hasImportedIdentity)
        {
            parsedDispId = comDispId!.Value;
        }
        if (!hasImportedIdentity &&
            !VBComDispatch.TryGetComEventIdentity(
                source,
                eventName,
                out parsedInterfaceId,
                out parsedDispId))
        {
            return false;
        }

        try
        {
            var callback = new EventCallback(target, method);
            var parameterTypes = method.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
            var delegateType = GetComEventDelegateType(method);
            @delegate = CreateEventDelegate(delegateType, parameterTypes, callback);
            ComEventsHelper.Combine(comSource, parsedInterfaceId, parsedDispId, @delegate);
            interfaceId = parsedInterfaceId;
            dispId = parsedDispId;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            NotSupportedException or
            TargetException or
            TargetInvocationException or
            COMException)
        {
            @delegate = null;
            return false;
        }
    }

    internal static Type GetComEventDelegateType(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        if (method.ReturnType != typeof(void))
        {
            throw new NotSupportedException("VB6 event handlers must use void-returning delegates.");
        }

        var parameters = method.GetParameters();
        var key = string.Join(
            ";",
            parameters.Select(parameter =>
                (parameter.ParameterType.AssemblyQualifiedName ??
                 parameter.ParameterType.FullName ??
                 parameter.ParameterType.Name) +
                ":" + GetComEventMarshalKind(parameter)));
        lock (ComEventDelegateTypeSync)
        {
            if (ComEventDelegateTypes.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var type = ComEventDelegateModule.DefineType(
                "VB6ComEventDelegate_" + ++_nextComEventDelegateTypeId,
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
                typeof(void),
                parameters.Select(parameter => parameter.ParameterType).ToArray());
            invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = invoke.DefineParameter(
                    index + 1,
                    ParameterAttributes.None,
                    parameters[index].Name ?? "arg" + index);
                ApplyComEventMarshal(parameter, parameters[index]);
            }

            var created = type.CreateType()
                ?? throw new InvalidOperationException("COM event delegate type creation failed.");
            ComEventDelegateTypes.Add(key, created);
            return created;
        }
    }

    private static string GetComEventMarshalKind(ParameterInfo parameter)
    {
        if (parameter.GetCustomAttribute<MarshalAsAttribute>()?.Value == UnmanagedType.Struct)
        {
            return "variant";
        }

        var elementType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()!
            : parameter.ParameterType;
        return elementType == typeof(bool)
            ? "variant-bool"
            : elementType == typeof(string)
                ? "bstr"
                : "default";
    }

    private static void ApplyComEventMarshal(ParameterBuilder parameter, ParameterInfo source)
    {
        var elementType = source.ParameterType.IsByRef
            ? source.ParameterType.GetElementType()!
            : source.ParameterType;
        var unmanagedType = source.GetCustomAttribute<MarshalAsAttribute>()?.Value == UnmanagedType.Struct
            ? UnmanagedType.Struct
            : elementType == typeof(bool)
                ? UnmanagedType.VariantBool
                : elementType == typeof(string)
                    ? UnmanagedType.BStr
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

    private static Delegate CreateEventDelegate(Type eventHandlerType, EventCallback callback)
    {
        var invoke = eventHandlerType.GetMethod("Invoke")
            ?? throw new InvalidOperationException($"Event delegate '{eventHandlerType}' has no Invoke method.");
        if (invoke.ReturnType != typeof(void))
        {
            throw new NotSupportedException("VB6 event handlers must use void-returning delegates.");
        }

        var eventParameters = invoke.GetParameters();
        return CreateEventDelegate(
            eventHandlerType,
            eventParameters.Select(parameter => parameter.ParameterType).ToArray(),
            callback);
    }

    private static Delegate CreateEventDelegate(
        Type delegateType,
        IReadOnlyList<Type> eventParameterTypes,
        EventCallback callback)
    {
        var dynamicParameters = new[] { typeof(EventCallback) }
            .Concat(eventParameterTypes)
            .ToArray();
        var dynamicMethod = new DynamicMethod(
            "VB6EventAdapter",
            typeof(void),
            dynamicParameters,
            typeof(VBEvents).Module,
            skipVisibility: true);
        var generator = dynamicMethod.GetILGenerator();
        var arguments = generator.DeclareLocal(typeof(object[]));
        generator.Emit(OpCodes.Ldc_I4, eventParameterTypes.Count);
        generator.Emit(OpCodes.Newarr, typeof(object));
        generator.Emit(OpCodes.Stloc, arguments);

        for (var index = 0; index < eventParameterTypes.Count; index++)
        {
            generator.Emit(OpCodes.Ldloc, arguments);
            generator.Emit(OpCodes.Ldc_I4, index);
            EmitBoxedArgument(generator, eventParameterTypes[index], index + 1);
            generator.Emit(OpCodes.Stelem_Ref);
        }

        generator.Emit(OpCodes.Ldarg_0);
        generator.Emit(OpCodes.Ldloc, arguments);
        generator.Emit(OpCodes.Callvirt, typeof(EventCallback).GetMethod(nameof(EventCallback.Invoke))!);

        for (var index = 0; index < eventParameterTypes.Count; index++)
        {
            var parameterType = eventParameterTypes[index];
            if (!parameterType.IsByRef)
            {
                continue;
            }

            var elementType = parameterType.GetElementType()!;
            generator.Emit(OpCodes.Ldarg, index + 1);
            generator.Emit(OpCodes.Ldloc, arguments);
            generator.Emit(OpCodes.Ldc_I4, index);
            generator.Emit(OpCodes.Ldelem_Ref);
            if (elementType.IsValueType)
            {
                generator.Emit(OpCodes.Unbox_Any, elementType);
                generator.Emit(OpCodes.Stobj, elementType);
            }
            else
            {
                generator.Emit(OpCodes.Castclass, elementType);
                generator.Emit(OpCodes.Stind_Ref);
            }
        }

        generator.Emit(OpCodes.Ret);
        return dynamicMethod.CreateDelegate(delegateType, callback);
    }

    private static void EmitBoxedArgument(ILGenerator generator, Type parameterType, int argumentIndex)
    {
        generator.Emit(OpCodes.Ldarg, argumentIndex);
        if (parameterType.IsByRef)
        {
            var elementType = parameterType.GetElementType()!;
            generator.Emit(OpCodes.Ldobj, elementType);
            parameterType = elementType;
        }

        if (parameterType.IsValueType)
        {
            generator.Emit(OpCodes.Box, parameterType);
        }
    }

    private sealed class EventCallback
    {
        private readonly object _target;
        private readonly MethodInfo _method;

        public EventCallback(object target, MethodInfo method)
        {
            _target = target;
            _method = method;
        }

        public void Invoke(object?[] arguments) => _method.Invoke(_target, arguments);
    }

    private static object? GetComObject(object source) =>
        source is IVBComObjectProvider provider ? provider.ComObject : source;

    private sealed class MethodSubscription
    {
        public MethodSubscription(
            object source,
            string eventName,
            object target,
            string methodName,
            Action<object?[]>? handler,
            IVB6Host? host,
            EventInfo? eventInfo = null,
            Delegate? @delegate = null,
            Guid? comInterfaceId = null,
            int? comDispId = null)
        {
            Source = source;
            EventName = eventName;
            Target = target;
            MethodName = methodName;
            Handler = handler;
            Host = host;
            EventInfo = eventInfo;
            Delegate = @delegate;
            ComInterfaceId = comInterfaceId;
            ComDispId = comDispId;
        }

        public object Source { get; }
        public string EventName { get; }
        public object Target { get; }
        public string MethodName { get; }
        public Action<object?[]>? Handler { get; }
        public IVB6Host? Host { get; }
        public EventInfo? EventInfo { get; }
        public Delegate? Delegate { get; }
        public Guid? ComInterfaceId { get; }
        public int? ComDispId { get; }
    }
}
