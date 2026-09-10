using System.Reflection;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// How a generated control finds the half of itself that needs a screen.
///
/// The layer boundary is the whole difficulty. <c>VB6.Runtime</c> must not know
/// <c>VB6.Runtime.WinForms</c> -- the runtime is the host-neutral contract and the WinForms
/// assembly is one implementation of it, and a reference the other way would make every headless
/// program drag in a UI framework. But a control activated by a foreign container has nobody to
/// call <see cref="Register"/> for it: the container creates the class through COM, and no startup
/// code of ours runs at all.
///
/// So the companion is discovered **by name**, once, on first need. That is a deliberate
/// reflection seam and it is the same shape the emitter already uses for this assembly, which it
/// calls the optional WinForms runtime companion. If it is not beside the component, the control
/// stays headless and every member that needs a window says so -- which is the honest outcome and
/// the one a container can act on.
/// </summary>
[SupportedOSPlatform("windows")]
public static class VBControlPresentationHost
{
    private const string CompanionAssemblyName = "VB6.Runtime.WinForms";
    private const string CompanionInstallerType = "VB6.Runtime.WinForms.WinFormsControlPresentationFactory";
    private const string CompanionInstallerMethod = "Install";

    private static readonly object Sync = new();
    private static Func<VBComUserControl, IVBControlPresentation?>? _factory;
    private static bool _companionSearched;

    /// <summary>
    /// Installs the factory a host provides. Called by the companion assembly, or directly by a
    /// program that hosts generated controls itself.
    /// </summary>
    public static void Register(Func<VBComUserControl, IVBControlPresentation?> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (Sync)
        {
            _factory = factory;
        }
    }

    /// <summary>
    /// Declares that this process has no presentation, without searching for a companion. A test
    /// that measures the headless answers uses it so the result does not depend on which files
    /// happen to sit in its output directory.
    /// </summary>
    public static void SuppressForThisProcess() => Register(_ => null);

    /// <summary>Forgets the installed factory. For tests that measure the headless answers.</summary>
    public static void Reset()
    {
        lock (Sync)
        {
            _factory = null;
            _companionSearched = false;
        }
    }

    /// <summary>True while a host has made a presentation available.</summary>
    public static bool IsAvailable
    {
        get
        {
            lock (Sync)
            {
                return _factory is not null;
            }
        }
    }

    internal static IVBControlPresentation? TryCreate(VBComUserControl control)
    {
        Func<VBComUserControl, IVBControlPresentation?>? factory;
        lock (Sync)
        {
            if (_factory is null && !_companionSearched)
            {
                // Once, whatever the outcome. A missing companion is the normal state of a
                // headless process, and retrying the load on every draw call would turn it into a
                // measurable cost for programs that will never have a window.
                _companionSearched = true;
                TryInstallCompanion();
            }

            factory = _factory;
        }

        return factory?.Invoke(control);
    }

    private static void TryInstallCompanion()
    {
        try
        {
            var assembly = Assembly.Load(CompanionAssemblyName);
            var installer = assembly.GetType(CompanionInstallerType, throwOnError: false);
            installer
                ?.GetMethod(CompanionInstallerMethod, BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, null);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
                FileLoadException or
                BadImageFormatException or
                TargetInvocationException)
        {
            // No companion, a companion for another architecture, or one that failed to install.
            // None of the three is an error here: the control simply has no presentation, and it
            // reports that from the members that need one instead of throwing out of a COM call
            // the container made for another reason.
        }
    }
}
