using System.Diagnostics;
using System.ComponentModel;
using VB6.Emit.Managed;

namespace VB6.Compiler;

/// <summary>Installs or removes the native registration of an SDK-generated .NET COM host.</summary>
public static class ManagedComRegistration
{
    public static ManagedComRegistrationResult Execute(
        string comHostPath,
        ManagedPlatform platform = ManagedPlatform.AnyCpu,
        bool unregister = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comHostPath);

        var fullPath = Path.GetFullPath(comHostPath);
        if (!File.Exists(fullPath))
        {
            return Failure(
                string.Empty,
                -1,
                $"COM host '{fullPath}' was not found.");
        }

        if (!fullPath.EndsWith(".comhost.dll", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                string.Empty,
                -1,
                "COM registration requires an SDK-generated '.comhost.dll' file.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return Failure(
                string.Empty,
                -1,
                "COM registration is supported only on Windows.");
        }

        var toolPath = GetRegsvr32Path(platform);
        if (!File.Exists(toolPath))
        {
            return Failure(
                toolPath,
                -1,
                $"The matching regsvr32 executable '{toolPath}' was not found.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // /s is important here: regsvr32 otherwise opens a native message box for many
            // registration failures, which is unsuitable for compiler and CI invocations.
            startInfo.ArgumentList.Add("/s");
            if (unregister)
            {
                startInfo.ArgumentList.Add("/u");
            }

            startInfo.ArgumentList.Add(fullPath);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Failure(toolPath, -1, "Could not start regsvr32.");
            }

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new ManagedComRegistrationResult(
                process.ExitCode == 0,
                toolPath,
                process.ExitCode,
                standardOutput,
                standardError);
        }
        catch (Win32Exception exception)
        {
            return Failure(toolPath, -1, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Failure(toolPath, -1, exception.Message);
        }
    }

    public static string GetRegsvr32Path(ManagedPlatform platform)
    {
        if (!OperatingSystem.IsWindows())
        {
            return "regsvr32.exe";
        }

        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var use32BitTool = platform == ManagedPlatform.X86 ||
            (platform == ManagedPlatform.AnyCpu && !Environment.Is64BitOperatingSystem);
        var systemDirectory = use32BitTool && Environment.Is64BitOperatingSystem
            ? "SysWOW64"
            : "System32";
        return Path.Combine(windowsDirectory, systemDirectory, "regsvr32.exe");
    }

    private static ManagedComRegistrationResult Failure(
        string toolPath,
        int exitCode,
        string error) =>
        new(false, toolPath, exitCode, string.Empty, error);
}

public sealed record ManagedComRegistrationResult(
    bool Success,
    string ToolPath,
    int ExitCode,
    string StandardOutput,
    string StandardError);
