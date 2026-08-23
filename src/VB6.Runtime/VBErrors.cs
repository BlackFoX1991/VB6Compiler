namespace VB6.Runtime;

/// <summary>Thread-local VB6 Err state shared by generated procedures on the current thread.</summary>
public static class VBErrors
{
    [ThreadStatic]
    private static ErrorState? _state;

    public static int NumberValue() => _state?.Number ?? 0;
    public static string DescriptionValue() => _state?.Description ?? string.Empty;
    public static string SourceValue() => _state?.Source ?? string.Empty;
    public static int LineNumberValue() => _state?.LineNumber ?? 0;
    public static int ResumeIndexValue() => _state?.ResumeIndex ?? -1;

    public static void Clear() => _state = null;

    public static void Raise(int number, string source, string description, string helpFile, int helpContext)
    {
        _state = new ErrorState(number, source, description, helpFile, helpContext);
        throw new VB6RaisedError(number, description);
    }

    public static void Set(Exception exception)
    {
        Set(exception, -1);
    }

    public static void Set(Exception exception, int resumeIndex)
    {
        if (exception is VB6RaisedError raised && _state is not null &&
            _state.Number == raised.Number &&
            string.Equals(_state.Description, raised.Description, StringComparison.Ordinal))
        {
            _state = _state with { ResumeIndex = resumeIndex };
            return;
        }

        _state = new ErrorState(
            Number: exception is VB6TypeMismatchException ? 13 : 5,
            Source: exception.GetType().Name,
            Description: exception.Message,
            HelpFile: string.Empty,
            HelpContext: 0,
            ResumeIndex: resumeIndex);
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
