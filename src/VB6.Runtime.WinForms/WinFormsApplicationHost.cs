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
        _previousHost = VBInteraction.Host;
        _activeHost = new WinFormsHost(preferNativeActiveX: true);
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
