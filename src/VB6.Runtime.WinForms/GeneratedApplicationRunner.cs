using System.Reflection;
using System.Windows.Forms;
using VB6.Runtime;

namespace VB6.Runtime.WinForms;

/// <summary>
/// Runs a generated VB6 managed application inside the WinForms host. This is intentionally a
/// separate launcher contract so the compiler-produced assembly remains usable headless or from
/// another host such as Visual Studio.
/// </summary>
public static class GeneratedApplicationRunner
{
    public static int Run(string assemblyPath, string[]? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The generated VB6 assembly was not found.", fullPath);
        }

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return RunOnSta(fullPath, arguments ?? Array.Empty<string>());
        }

        var result = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = RunOnSta(fullPath, arguments ?? Array.Empty<string>());
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw failure;
        }

        return result;
    }

    private static int RunOnSta(string assemblyPath, string[] arguments)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var assembly = Assembly.LoadFrom(assemblyPath);
        var entryPoint = assembly.EntryPoint
            ?? throw new InvalidOperationException("The generated assembly has no application entry point.");
        var parameters = entryPoint.GetParameters();
        var invokeArguments = parameters.Length switch
        {
            0 => null,
            1 when parameters[0].ParameterType == typeof(string[]) => new object?[] { arguments },
            _ => throw new InvalidOperationException("The generated entry point has an unsupported signature.")
        };

        using var host = new WinFormsHost();
        var previousHost = VBInteraction.Host;
        try
        {
            VBInteraction.Host = host;
            entryPoint.Invoke(null, invokeArguments);
            return host.RunMessageLoop();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }
}
