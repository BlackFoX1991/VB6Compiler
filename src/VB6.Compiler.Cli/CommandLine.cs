using VB6.Compiler;
using VB6.Emit.Managed;
using VB6.Runtime;

namespace VB6.Compiler.Cli;

internal enum CliCommand
{
    /// <summary>No command: describe the input.</summary>
    Analyze,
    Report,
    DumpIr,
    EmitLlvm,
    EmitAssembly,
    RegisterCom,
    UnregisterCom,
    WriteInputManifest,

    /// <summary>A first argument that is not a command at all.</summary>
    Unknown
}

/// <summary>
/// One parse of the command line for every input kind. The three input kinds -- source file,
/// <c>.vbp</c> and <c>.vbg</c> -- accept different commands but the same option grammar, and
/// that grammar used to be written out once per kind. A new option then meant three edits, and a
/// forgotten one showed up only in the slow process tests.
/// </summary>
internal sealed record CommandLineOptions(
    string InputPath,
    CliCommand Command,
    string? OutputPath,
    ManagedPlatform Platform,
    VBCompatibilityProfile CompatibilityProfile,
    bool ComHost,
    bool ComManifest,
    string? LlvmArchitecture,
    int ArgumentCount)
{
    /// <summary>
    /// Legacy VB6 projects are 32-bit: their ActiveX controls cannot load into a 64-bit process.
    /// A single source file has no project boundary to inherit that from and stays AnyCPU.
    /// </summary>
    internal static ManagedPlatform GetDefaultPlatform(string inputPath) =>
        Path.GetExtension(inputPath) is { } extension &&
        (string.Equals(extension, ".vbp", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(extension, ".vbg", StringComparison.OrdinalIgnoreCase))
            ? ManagedPlatform.X86
            : ManagedPlatform.AnyCpu;
}

internal static class CommandLineParser
{
    public static bool TryParse(string[] arguments, out CommandLineOptions options)
    {
        options = null!;
        var inputPath = arguments[0];
        var platform = CommandLineOptions.GetDefaultPlatform(inputPath);
        var command = arguments.Length < 2 ? CliCommand.Analyze : ParseCommand(arguments[1]);
        if (command == CliCommand.Unknown)
        {
            return false;
        }

        string? outputPath = null;
        string? llvmArchitecture = null;
        var compatibilityProfile = VBCompatibilityProfile.Deterministic;
        var comHost = false;
        var comManifest = false;
        ManagedPlatform? selectedPlatform = null;

        // Every command that writes something takes its destination in the same position, and
        // --dump-ir is the one where it is optional.
        // A bare --compatibility is not a command with options after it; it *is* the option, so
        // the scan has to start on it rather than past it.
        var index = arguments.Length < 2
            ? arguments.Length
            : string.Equals(arguments[1], "--compatibility", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        if (command is CliCommand.EmitAssembly or CliCommand.EmitLlvm or CliCommand.WriteInputManifest)
        {
            if (arguments.Length < 3)
            {
                Console.Error.WriteLine($"'{arguments[1]}' expects an output path.");
                return false;
            }

            outputPath = arguments[2];
            index = 3;
        }
        else if (command == CliCommand.DumpIr &&
                 arguments.Length > 2 &&
                 !arguments[2].StartsWith("--", StringComparison.Ordinal))
        {
            outputPath = arguments[2];
            index = 3;
        }
        else if (command == CliCommand.EmitLlvm)
        {
            index = 3;
        }

        // The LLVM backend takes its architecture positionally rather than as a named option.
        if (command == CliCommand.EmitLlvm &&
            index < arguments.Length &&
            arguments[index] is "--x86" or "--x64")
        {
            llvmArchitecture = arguments[index++];
        }

        for (; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--compatibility", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Length ||
                    !TryParseCompatibilityProfile(arguments[++index], out compatibilityProfile))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(argument, "--com-host", StringComparison.OrdinalIgnoreCase))
            {
                comHost = true;
                continue;
            }

            if (string.Equals(argument, "--com-manifest", StringComparison.OrdinalIgnoreCase))
            {
                comManifest = true;
                comHost = true;
                continue;
            }

            if (argument is "--x86" or "--x64" or "--anycpu")
            {
                if (!TryParseManagedPlatform(argument, out var parsedPlatform))
                {
                    return false;
                }

                if (selectedPlatform is not null)
                {
                    Console.Error.WriteLine("Managed architecture was specified more than once.");
                    return false;
                }

                selectedPlatform = parsedPlatform;
                platform = parsedPlatform;
                continue;
            }

            Console.Error.WriteLine(
                $"Unknown option '{argument}'. Use --x86, --x64, --anycpu, --compatibility, --com-host or --com-manifest.");
            return false;
        }

        // The VB6Sp6 profile is defined against the original 32-bit runtime, so it selects x86
        // when nothing else was asked for and rejects any other explicit choice.
        if (compatibilityProfile == VBCompatibilityProfile.VB6Sp6)
        {
            if (selectedPlatform is null)
            {
                platform = ManagedPlatform.X86;
            }

            if (platform != ManagedPlatform.X86)
            {
                Console.Error.WriteLine("The vb6-sp6 compatibility profile supports x86 targets only.");
                return false;
            }
        }

        options = new CommandLineOptions(
            inputPath,
            command,
            outputPath,
            platform,
            compatibilityProfile,
            comHost,
            comManifest,
            llvmArchitecture,
            arguments.Length);
        return true;
    }

    private static CliCommand ParseCommand(string argument) => argument.ToLowerInvariant() switch
    {
        "--report" => CliCommand.Report,
        "--dump-ir" => CliCommand.DumpIr,
        "--emit-llvm" => CliCommand.EmitLlvm,
        "--emit-assembly" => CliCommand.EmitAssembly,
        "--register-com" => CliCommand.RegisterCom,
        "--unregister-com" => CliCommand.UnregisterCom,
        "--write-input-manifest" => CliCommand.WriteInputManifest,

        // A bare --compatibility keeps its meaning: analyze, but with that profile.
        "--compatibility" => CliCommand.Analyze,
        _ => CliCommand.Unknown
    };

    private static bool TryParseCompatibilityProfile(
        string? value,
        out VBCompatibilityProfile compatibilityProfile)
    {
        compatibilityProfile = value?.ToLowerInvariant() switch
        {
            "deterministic" => VBCompatibilityProfile.Deterministic,
            "vb6-sp6" => VBCompatibilityProfile.VB6Sp6,
            _ => (VBCompatibilityProfile)(-1)
        };

        if ((int)compatibilityProfile >= 0)
        {
            return true;
        }

        Console.Error.WriteLine($"Unknown compatibility profile '{value}'. Use deterministic or vb6-sp6.");
        return false;
    }

    private static bool TryParseManagedPlatform(string argument, out ManagedPlatform platform)
    {
        platform = argument.ToLowerInvariant() switch
        {
            "--x86" => ManagedPlatform.X86,
            "--x64" => ManagedPlatform.X64,
            "--anycpu" => ManagedPlatform.AnyCpu,
            _ => (ManagedPlatform)(-1)
        };

        if ((int)platform >= 0)
        {
            return true;
        }

        Console.Error.WriteLine($"Unknown managed architecture '{argument}'. Use --x86, --x64 or --anycpu.");
        return false;
    }
}
