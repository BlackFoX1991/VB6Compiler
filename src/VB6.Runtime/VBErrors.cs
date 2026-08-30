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
                OverflowException => 6,
                DivideByZeroException => 11,
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
        Clear();
        _activeHandlerDepth = -1;
    }

    public static void InvalidResume() =>
        throw new VB6RaisedError(20, "Resume without an active error.");

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
