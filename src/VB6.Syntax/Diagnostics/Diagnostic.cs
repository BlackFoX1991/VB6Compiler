using VB6.Syntax.Text;

namespace VB6.Syntax.Diagnostics;

public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    TextSpan Span,
    string? FilePath = null)
{
    public override string ToString()
    {
        var location = FilePath is null ? Span.ToString() : $"{FilePath}:{Span}";
        return $"{Severity} {Code} {location}: {Message}";
    }
}
