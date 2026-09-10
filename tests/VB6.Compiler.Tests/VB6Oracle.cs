using System.Diagnostics;
using System.Text;
using VB6.Emit.Managed;
using VB6.Runtime;

namespace VB6.Compiler.Tests;

/// <summary>
/// The original compiler, asked the same question as ours.
///
/// Until 2026-09-10 this repository had no oracle, and the matrix says so in two places: the
/// `oracle-verified` axis was defined and deliberately left unused, and R1 recorded that the
/// `Get`/`Put` contracts rest on VBA documentation rather than on a real VB6 run. A portable
/// **VB6 SP6** (`VB6.EXE 6.00.9782`) changed that, and the first question put to it falsified a
/// claim that had been carried as `documented-verified` for months.
///
/// Which is the reason this class exists rather than a handful of one-off probes: a measurement
/// against our own understanding is a regression proof, not a contract proof. Only the original
/// settles what VB6 does.
///
/// The shape of a case is deliberately crude and therefore hard to fool: the same source is
/// compiled by both compilers, both programs are run, and their **output files** are compared. Not
/// their exit codes, not their diagnostics -- the values they produce. A program writes to a file
/// because a compiled VB6 program has no console; `Debug.Print` goes nowhere there.
/// </summary>
internal static class VB6Oracle
{
    /// <summary>The file every oracle program writes its answers into.</summary>
    internal const string AnswerFileName = "antworten.txt";

    /// <summary>
    /// Where <c>VB6.EXE</c> lives. Absent means this machine has no oracle, and a case skips
    /// itself -- unless the run demands one.
    /// </summary>
    internal static string? ExecutablePath
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("VB6_ORACLE_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                // Ein Verzeichnis ist die bequemere Angabe und die haeufigere Fehlform; beide
                // werden angenommen, statt den Aufrufer an einer Pfadkonvention scheitern zu
                // lassen.
                if (File.Exists(configured))
                {
                    return configured;
                }

                var inDirectory = Path.Combine(configured, "VB6.EXE");
                return File.Exists(inDirectory) ? inDirectory : null;
            }

            return null;
        }
    }

    /// <summary>
    /// True while the run insists on the oracle. The switch turns "skipped" into "reported",
    /// which is the same rule the native OCX path uses: a skipped case is not a passed one.
    /// </summary>
    internal static bool IsRequired =>
        string.Equals(
            Environment.GetEnvironmentVariable("VB6_REQUIRE_ORACLE"),
            "1",
            StringComparison.Ordinal);

    /// <summary>
    /// Answers whether this machine can be asked at all, and reports rather than skips when the
    /// run demanded an oracle.
    /// </summary>
    internal static bool IsAvailable(out string? reason)
    {
        reason = null;
        if (!OperatingSystem.IsWindows())
        {
            reason = "Das Original ist eine Windows-Anwendung.";
            return false;
        }

        if (ExecutablePath is null)
        {
            reason =
                "Kein Original gefunden. VB6_ORACLE_PATH auf VB6.EXE oder ihr Verzeichnis setzen.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Puts one question to both compilers and returns both answers.
    ///
    /// <paramref name="body"/> is the inside of <c>Sub Main</c>; the surrounding module, the
    /// output file and the path handling are added here. Two rules are baked in because the
    /// original enforces them and cost the first two attempts:
    ///
    /// <list type="bullet">
    /// <item>The module cannot be called <c>Main</c> when the startup is <c>Sub Main</c>.</item>
    /// <item>VB6 is case-insensitive, so no local may collide with the helper's name.</item>
    /// <item>The module name and the project name must differ.</item>
    /// </list>
    ///
    /// All three came from the original refusing to build, and none of the three is enforced by
    /// this compiler -- which is itself a finding rather than a detail of this harness.
    ///
    /// Ours is compiled with <c>--compatibility vb6-sp6</c>. Locale contracts are profile-bound by
    /// decision -- the original uses the system LCID, our default profile deliberately does not --
    /// so comparing against the deterministic profile would report a decimal comma as a defect.
    /// </summary>
    internal static OracleComparison Ask(string body, string? declarations = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        var executable = ExecutablePath
            ?? throw new InvalidOperationException("Kein Original vorhanden; erst IsAvailable fragen.");

        var directory = Path.Combine(
            Path.GetTempPath(), "VB6Oracle", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            WriteProject(directory, body, declarations);

            var original = RunOriginal(executable, directory);
            var ours = RunOurs(directory);
            return new OracleComparison(original, ours);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    /// <summary>
    /// The project both compilers read. It is written with CRLF endings and an
    /// <c>Attribute VB_Name</c> line because that is what the original expects from a `.bas`.
    /// </summary>
    private static void WriteProject(string directory, string body, string? declarations)
    {
        var source = new StringBuilder();
        source.AppendLine("Attribute VB_Name = \"Sonde\"");
        source.AppendLine("Option Explicit");
        source.AppendLine();
        source.AppendLine("Private vb6OracleFile As Integer");
        source.AppendLine();
        if (!string.IsNullOrWhiteSpace(declarations))
        {
            source.AppendLine(declarations);
            source.AppendLine();
        }

        // Der Name ist absichtlich sperrig: VB6 ist case-insensitiv, und ein kurzer Name wie
        // Zeig kollidiert mit einer gleichnamigen lokalen Variablen im Fallkoerper.
        source.AppendLine(
            "Public Sub Vb6OracleSay(ByVal Bezeichnung As String, ByVal Wert As Variant)");
        source.AppendLine("    Print #vb6OracleFile, Bezeichnung & \"=\" & Wert");
        source.AppendLine("End Sub");
        source.AppendLine();
        source.AppendLine(
            "Public Sub Vb6OracleType(ByVal Bezeichnung As String, ByVal Wert As Variant)");
        source.AppendLine("    Print #vb6OracleFile, Bezeichnung & \"=\" & TypeName(Wert)");
        source.AppendLine("End Sub");
        source.AppendLine();
        source.AppendLine("Sub Main()");
        source.AppendLine("    Dim vb6OraclePath As String");
        source.AppendLine("    vb6OraclePath = App.Path");
        source.AppendLine(
            "    If Right$(vb6OraclePath, 1) <> \"\\\" Then vb6OraclePath = vb6OraclePath & \"\\\"");
        source.AppendLine("    vb6OracleFile = FreeFile");
        source.AppendLine(
            $"    Open vb6OraclePath & \"{AnswerFileName}\" For Output As #vb6OracleFile");
        source.AppendLine();
        source.AppendLine(body);
        source.AppendLine();
        source.AppendLine("    Close #vb6OracleFile");
        source.AppendLine("End Sub");

        File.WriteAllText(
            Path.Combine(directory, "Sonde.bas"),
            source.ToString().ReplaceLineEndings("\r\n"),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        File.WriteAllText(
            Path.Combine(directory, "Sonde.vbp"),
            string.Join(
                "\r\n",
                "Type=Exe",
                // Der Projektname muss sich vom Modulnamen unterscheiden. Das Original lehnt
                // 'Sonde' fuer beide mit 'Name conflicts with existing module, project, or object
                // library' ab -- die dritte Namensregel, die es uns beigebracht hat.
                "Name=\"Orakelsonde\"",
                "Module=Sonde; Sonde.bas",
                "Startup=\"Sub Main\"",
                "ExeName32=\"Sonde.exe\"",
                string.Empty),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Compiles with the original and runs the result.
    ///
    /// <c>/make</c> plus <c>/out</c> is the whole headless contract: the diagnostics land in a
    /// file and the exit code says whether it built. No dialog, no elevation, nothing registered.
    /// </summary>
    private static OracleAnswer RunOriginal(string executable, string directory)
    {
        var logPath = Path.Combine(directory, "vb6.log");
        var make = Run(
            executable,
            directory,
            "/make", Path.Combine(directory, "Sonde.vbp"), "/out", logPath);

        var log = File.Exists(logPath) ? File.ReadAllText(logPath).Trim() : string.Empty;
        var exePath = Path.Combine(directory, "Sonde.exe");
        if (make.ExitCode != 0 || !File.Exists(exePath))
        {
            return OracleAnswer.Failed($"VB6 /make endete mit {make.ExitCode}. {log}");
        }

        var run = Run(exePath, directory);
        var answers = Path.Combine(directory, AnswerFileName);
        return run.ExitCode == 0 && File.Exists(answers)
            ? OracleAnswer.From(File.ReadAllLines(answers))
            : OracleAnswer.Failed(
                $"Das erzeugte Programm endete mit {run.ExitCode}. {run.Output}".Trim());
    }

    /// <summary>Compiles the same project with this compiler and runs it.</summary>
    private static OracleAnswer RunOurs(string directory)
    {
        var output = Path.Combine(directory, "unser");
        Directory.CreateDirectory(output);

        var compilation = VBProjectCompilation.Create(
            Path.Combine(directory, "Sonde.vbp"),
            new VBCompilationOptions { CompatibilityProfile = VBCompatibilityProfile.VB6Sp6 });
        var emit = DirectManagedCompilation.EmitManaged(
            compilation,
            Path.Combine(output, "Sonde.exe"),
            new ManagedEmitOptions("Sonde", Platform: ManagedPlatform.X86));
        if (!emit.Success || emit.AssemblyPath is null)
        {
            return OracleAnswer.Failed(
                "Unser Compiler meldete: " +
                string.Join(
                    " | ",
                    emit.Lowering.Analysis.Diagnostics
                        .Select(diagnostic => diagnostic.ToString())
                        .Concat(
                            emit.BackendResult?.Diagnostics.Select(diagnostic => diagnostic.ToString())
                            ?? [])
                        .DefaultIfEmpty("(keine Diagnose)")));
        }

        // Der .NET-Host startet eine verwaltete Assembly, keinen Apphost. Ist die angeforderte
        // Ausgabe eine .exe, liegt die Assembly daneben und ManagedAssemblyPath nennt sie --
        // 'dotnet Sonde.exe' waere sonst ein Fehlstart, der wie ein Laufzeitfehler des Programms
        // aussieht.
        var assembly = emit.ManagedAssemblyPath ?? emit.AssemblyPath;
        if (X86HostPath is not { } host)
        {
            return OracleAnswer.Failed(
                "Kein 32-Bit-.NET-Host gefunden. Unsere Seite wird als x86 emittiert, weil das " +
                "vb6-sp6-Profil das verlangt; sie braucht deshalb dotnet.exe aus " +
                "'Program Files (x86)' oder DOTNET_ROOT_X86.");
        }

        var run = Run(host, output, assembly);
        var answers = Path.Combine(output, AnswerFileName);
        return run.ExitCode == 0 && File.Exists(answers)
            ? OracleAnswer.From(File.ReadAllLines(answers))
            : OracleAnswer.Failed(
                $"Unser Programm endete mit {run.ExitCode}. {run.Output}".Trim());
    }

    /// <summary>
    /// <c>ERROR_ELEVATION_REQUIRED</c>. On this machine the original carries the per-user
    /// <c>RUNASADMIN</c> compatibility flag, so starting it from a non-elevated process fails here
    /// rather than prompting: a redirected child process cannot show a UAC dialog, and nobody
    /// would see it if it could.
    /// </summary>
    private const int ElevationRequired = 740;

    /// <summary>
    /// The 32-bit .NET host.
    ///
    /// Our side of an oracle comparison is emitted x86 -- the <c>VB6Sp6</c> profile requires it,
    /// and comparing against a 32-bit original on another architecture would compare two different
    /// things anyway. A 64-bit test host therefore cannot start it: the failure is
    /// <c>FileLoadException: The assembly architecture is not compatible with the current process
    /// architecture</c>, reported by the child on its own output rather than as a test exception.
    ///
    /// <see cref="VB6TestProgram.DotnetHostPath"/> deliberately picks the host matching the *test*
    /// process, which is the right rule there and the wrong one here.
    /// </summary>
    private static string? X86HostPath
    {
        get
        {
            var roots = new[]
            {
                Environment.GetEnvironmentVariable("DOTNET_ROOT_X86"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)")
            };

            foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(root!, "dotnet.exe"),
                             Path.Combine(root!, "dotnet", "dotnet.exe")
                         })
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Runs a process and keeps what it said.
    ///
    /// Throwing the child's output away is the mistake that made the first failure here
    /// undiagnosable: an emitted program reports its unhandled error on **its own** stdout, not as
    /// an exception in the test host, so a bare exit code is all a caller would see. The project's
    /// notes call this out for exit code -532462766 in particular.
    /// </summary>
    private static (int ExitCode, string Output) Run(
        string fileName,
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = StartOrThrow(startInfo, fileName);

        // Asynchron lesen, bevor gewartet wird: Ein Kind, das mehr schreibt als in die Pipe passt,
        // blockiert sonst genau hier -- und der Testhost wartet auf ein Ende, das nie kommt.
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        // Ein haengender Compiler waere ein Dialog, den niemand sieht. Nach zwei Minuten ist die
        // Aussage 'es antwortet nicht' mehr wert als weiter zu warten.
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return (-1, "Der Prozess antwortete nicht und wurde beendet.");
        }

        var output = string.Join(
            " ",
            new[]
            {
                standardOutput.GetAwaiter().GetResult(),
                standardError.GetAwaiter().GetResult()
            }
            .Select(text => text.Trim())
            .Where(text => text.Length > 0));

        return (process.ExitCode, output);
    }

    /// <summary>
    /// Starts a process and turns the one failure that is a *situation* rather than a defect into
    /// a sentence a reader can act on.
    ///
    /// Without this the suite reports a bare <c>Win32Exception: Der angeforderte Vorgang erfordert
    /// erhöhte Rechte</c> from somewhere inside a test, which reads like a broken test rather than
    /// like "this run cannot reach the oracle".
    /// </summary>
    private static Process StartOrThrow(ProcessStartInfo startInfo, string fileName)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException($"'{fileName}' liess sich nicht starten.");
        }
        catch (System.ComponentModel.Win32Exception exception)
            when (exception.NativeErrorCode == ElevationRequired)
        {
            throw new OracleNeedsElevationException(fileName, exception);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            Thread.Sleep(100);
        }
    }
}

/// <summary>
/// The oracle is on this machine but out of reach: it is marked to require administrator rights,
/// and this test session is not elevated.
///
/// This is a deliberate state, not a defect. The compatibility flag belongs to the machine's owner
/// and stays untouched; an oracle run happens from an elevated session instead. The exception
/// exists so a case can say that in one sentence rather than surfacing a Win32 error code.
/// </summary>
internal sealed class OracleNeedsElevationException(string fileName, Exception inner)
    : Exception(
        $"Das Original ({fileName}) verlangt erhöhte Rechte und diese Sitzung ist nicht erhöht. " +
        "Ein Orakel-Lauf gehört in eine erhöhte Sitzung; die Kompatibilitätseinstellung " +
        "RUNASADMIN bleibt bewusst unangetastet.",
        inner);

/// <summary>One side's answers, keyed by the label the case gave them.</summary>
internal sealed record OracleAnswer(
    bool Success,
    string? Failure,
    IReadOnlyDictionary<string, string> Values)
{
    internal static OracleAnswer Failed(string failure) =>
        new(false, failure, new Dictionary<string, string>(StringComparer.Ordinal));

    internal static OracleAnswer From(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return new OracleAnswer(true, null, values);
    }
}

/// <summary>
/// Both answers side by side.
///
/// <see cref="Differences"/> reports only the labels both sides answered and answered differently.
/// A label one side never produced is a different failure -- a compile error or a crash -- and
/// surfaces through <see cref="Describe"/> rather than as a quiet mismatch.
/// </summary>
internal sealed record OracleComparison(OracleAnswer Original, OracleAnswer Ours)
{
    internal bool BothRan => Original.Success && Ours.Success;

    internal IReadOnlyList<(string Label, string Expected, string Actual)> Differences =>
        Original.Values
            .Where(pair => Ours.Values.TryGetValue(pair.Key, out var mine) && mine != pair.Value)
            .Select(pair => (pair.Key, pair.Value, Ours.Values[pair.Key]))
            .ToList();

    internal IReadOnlyList<string> MissingFromOurs =>
        Original.Values.Keys.Where(key => !Ours.Values.ContainsKey(key)).ToList();

    /// <summary>A table a reader can act on, for a failure message.</summary>
    internal string Describe()
    {
        var report = new StringBuilder();
        if (!Original.Success)
        {
            report.AppendLine("Original: " + Original.Failure);
        }

        if (!Ours.Success)
        {
            report.AppendLine("Wir: " + Ours.Failure);
        }

        foreach (var (label, expected, actual) in Differences)
        {
            report.AppendLine($"{label}: VB6={expected} wir={actual}");
        }

        foreach (var label in MissingFromOurs)
        {
            report.AppendLine($"{label}: unsere Ausgabe fehlt");
        }

        return report.ToString();
    }
}
