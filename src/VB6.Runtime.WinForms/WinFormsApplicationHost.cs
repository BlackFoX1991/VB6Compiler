using System.Reflection;
using System.Windows.Forms;
using VB6.Runtime;

namespace VB6.Runtime.WinForms;

/// <summary>
/// Self-hosting boundary used by generated Form applications. The generated assembly only calls
/// the portable reflection bridge in <see cref="VBInteraction"/>; this optional assembly owns the
/// WindowsDesktop dependency and the concrete host lifetime.
/// </summary>
public static class WinFormsApplicationHost
{
    private static WinFormsHost? _activeHost;
    private static IVB6Host? _previousHost;
    private static bool _ownsHost;

    public static void StartWinFormsHost()
    {
        if (_activeHost is not null)
        {
            return;
        }

        if (VBInteraction.Host is WinFormsHost existingHost)
        {
            _activeHost = existingHost;
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // VB6 does not offer to continue after an unhandled error -- it reports and ends. The
        // WinForms default catches the exception on the UI thread and shows a dialog with a
        // "Continue" button, which invents a choice VB6 never gives and hides the diagnosis behind
        // a "Details" button. The mode can only be set before the first control exists, which is
        // why it belongs to application startup and not to the message loop.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        _previousHost = VBInteraction.Host;
        var compatibilityProfile = VBCompatibilityProfileAttribute.FromAssembly(
            Assembly.GetEntryAssembly());
        _activeHost = new WinFormsHost(
            preferNativeActiveX: true,
            compatibilityProfile: compatibilityProfile);
        _ownsHost = true;
        VBInteraction.Host = _activeHost;
    }

    public static int RunWinFormsMessageLoop()
    {
        var host = _activeHost;
        if (host is null)
        {
            return 0;
        }

        try
        {
            return host.RunMessageLoop();
        }
        finally
        {
            if (_ownsHost)
            {
                VBInteraction.Host = _previousHost;
                host.Dispose();
            }

            _activeHost = null;
            _previousHost = null;
            _ownsHost = false;
        }
    }
}
