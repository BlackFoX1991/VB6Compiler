namespace VB6.Runtime;

public static class VBControlFlow
{
    /// <summary>Converts VB6's one-based On ... GoTo selector to a zero-based IL switch index.</summary>
    public static int OnGoToIndex(int value) => value <= 0 ? -1 : value - 1;

    /// <summary>
    /// Terminates a VB6 program. Hosts can intercept this boundary for an IDE or test harness;
    /// standalone managed applications use VB6's process-wide termination behavior.
    /// </summary>
    public static void EndProgram()
    {
        // End destroys objects but deliberately skips Class_Terminate. Mark this before a host
        // or Environment.Exit can execute the process-exit fallback registered by a class.
        VBObjectLifetime.SuppressPendingTerminatorsForEnd();

        if (EndProgramSink is { } sink)
        {
            sink();
            return;
        }

        Environment.Exit(0);
    }

    /// <summary>Optional host callback replacing process termination.</summary>
    public static Action? EndProgramSink { get; set; }
}
