using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>Thread-local VB6 Err state shared by generated procedures on the current thread.</summary>
public static class VBErrors
{
    [ThreadStatic]
    private static ErrorState? _state;

    [ThreadStatic]
    private static int _lineNumber;

    [ThreadStatic]
    private static int _procedureDepth;

    [ThreadStatic]
    private static int _activeHandlerDepth = -1;

    public static int NumberValue() => _state?.Number ?? 0;
    public static string DescriptionValue() => _state?.Description ?? string.Empty;
    public static string SourceValue() => _state?.Source ?? string.Empty;
    public static string HelpFileValue() => _state?.HelpFile ?? string.Empty;
    public static int HelpContextValue() => _state?.HelpContext ?? 0;
    public static int LastDllErrorValue() => Marshal.GetLastPInvokeError();
    public static int LineNumberValue() => _state?.LineNumber ?? 0;
    public static int ResumeIndexValue() => _state?.ResumeIndex ?? -1;

    /// <summary>Enters the error-state scope of one generated VB6 procedure.</summary>
    public static void EnterProcedure() => _procedureDepth++;

    /// <summary>Leaves a generated VB6 procedure and deactivates its pending handler.</summary>
    public static void ExitProcedure()
    {
        if (_activeHandlerDepth == _procedureDepth)
        {
            _activeHandlerDepth = -1;
        }

        if (_procedureDepth > 0)
        {
            _procedureDepth--;
        }
    }

    public static void Clear()
    {
        _state = null;
        _lineNumber = 0;
    }

    /// <summary>
    /// Records the most recently executed numeric VB6 line label. <c>Erl</c> reports this value
    /// when a later statement raises an error; ordinary named labels do not change it.
    /// </summary>
    public static void SetLineNumber(int lineNumber)
    {
        _lineNumber = lineNumber > 0 ? lineNumber : 0;
    }

    public static void Raise(int number, string source, string description, string helpFile, int helpContext)
    {
        _state = new ErrorState(
            number,
            source,
            description,
            helpFile,
            helpContext,
            LineNumber: _lineNumber);
        throw new VB6RaisedError(number, description);
    }

    public static void Set(Exception exception)
    {
        Set(exception, -1);
    }

    public static void Set(Exception exception, int resumeIndex)
    {
        Set(exception, resumeIndex, hasHandler: false);
    }

    /// <summary>
    /// Stores an exception raised by a protected statement. A label-directed handler becomes
    /// active only when control is actually transferred to it; a second error while that handler
    /// is active must escape the procedure instead of recursively re-entering the same label.
    /// </summary>
    public static void Set(Exception exception, int resumeIndex, bool hasHandler)
    {
        if (_activeHandlerDepth == _procedureDepth && _procedureDepth > 0)
        {
            _activeHandlerDepth = -1;
            if (_procedureDepth > 0)
            {
                _procedureDepth--;
            }

            throw exception;
        }

        if (exception is VB6RaisedError raised && _state is not null &&
            _state.Number == raised.Number &&
            string.Equals(_state.Description, raised.Description, StringComparison.Ordinal))
        {
            _state = _state with { ResumeIndex = resumeIndex };
            if (hasHandler)
            {
                _activeHandlerDepth = _procedureDepth;
            }
            return;
        }

        _state = new ErrorState(
            Number: exception switch
            {
                VB6MissingArgumentException => 448,
                VB6TypeMismatchException => 13,
                VB6RuntimeErrorException runtimeError => runtimeError.Number,
                VB6RaisedError raisedError => raisedError.Number,
                OverflowException => 6,
                DivideByZeroException => 11,
                FormatException or InvalidCastException => 13,
                // Nur IndexOutOfRange, nicht ArgumentOutOfRange: Letztere deckt auch
                // Faelle wie Space(-1) ab, fuer die VB6 weiterhin 5 meldet.
                IndexOutOfRangeException => 9,
                MissingMemberException => 438,
                // Ein Mitgliedszugriff auf eine nicht gesetzte Objektvariable ist in VB6
                // Fehler 91. Der frueh gebundene Pfad ruft dabei auf null und erzeugt die
                // CLR-Ausnahme, der spaet gebundene wirft sie in RequireTarget selbst --
                // beide Wege treffen sich hier.
                NullReferenceException => 91,
                // Ein fehlender Pfad ist in VB6 Fehler 53. Die Verzeichnisvariante zaehlt mit:
                // VB6 unterscheidet beim Oeffnen nicht, welcher Teil des Pfades fehlt.
                FileNotFoundException or DirectoryNotFoundException => 53,
                _ => 5
            },
            Source: exception.GetType().Name,
            Description: exception.Message,
            HelpFile: string.Empty,
            HelpContext: 0,
            LineNumber: _lineNumber,
            ResumeIndex: resumeIndex);

        if (hasHandler)
        {
            _activeHandlerDepth = _procedureDepth;
        }
    }

    /// <summary>Clears Err and deactivates the current handler for a Resume operation.</summary>
    public static void Resume()
    {
        if (_state is null || _activeHandlerDepth != _procedureDepth)
        {
            InvalidResume();
        }

        Clear();
        _activeHandlerDepth = -1;
    }

    public static void InvalidResume() =>
        throw new VB6RaisedError(20, "Resume without an active error.");

    /// <summary>
    /// True while a Resume may run, that is while this procedure has an error its handler is
    /// still working on. The bare Resume and Resume Next forms ask before they act: their
    /// dispatch is a switch outside every protected region, so an exception raised there could
    /// never be caught by an enclosing On Error Resume Next.
    /// </summary>
    public static bool HasActiveResume() => _state is not null && _activeHandlerDepth == _procedureDepth;

    /// <summary>
    /// Records the documented error 20 for a Resume that has no error to return from, without
    /// raising it. An enclosing On Error Resume Next then observes it through Err and carries on
    /// with the next statement, exactly as it would for any other error.
    /// </summary>
    public static void RecordResumeWithoutError() =>
        Set(new VB6RaisedError(20, "Resume without an active error."), -1, hasHandler: false);

    private sealed record ErrorState(
        int Number,
        string Source,
        string Description,
        string HelpFile,
        int HelpContext,
        int LineNumber = 0,
        int ResumeIndex = -1);
}

public sealed class VB6TypeMismatchException : Exception
{
    public VB6TypeMismatchException(string description)
        : base(description)
    {
    }
}

public sealed class VB6MissingArgumentException : Exception
{
    public VB6MissingArgumentException()
        : base("The optional Variant argument was not supplied.")
    {
    }
}

/// <summary>Represents a runtime error with a specific VB6 error number.</summary>
public sealed class VB6RuntimeErrorException : Exception
{
    public VB6RuntimeErrorException(int number, string description)
        : base(description)
    {
        Number = number;
    }

    public int Number { get; }
}

public sealed class VB6RaisedError : Exception
{
    public VB6RaisedError(int number, string description)
        : base($"VB6 error {number}: {description}")
    {
        Number = number;
        Description = description;
    }

    public int Number { get; }
    public string Description { get; }
}
