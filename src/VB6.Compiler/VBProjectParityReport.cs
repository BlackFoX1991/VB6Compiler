using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using VB6.ProjectSystem;
using VB6.Syntax.Diagnostics;

namespace VB6.Compiler;

/// <summary>
/// Measures how much of a VB6 project the compiler currently understands.
///
/// Raw diagnostic counts are a poor progress metric because a single unsupported construct
/// derails the parser and produces a cascade of follow-on errors. The report therefore counts
/// <em>files</em> that analyze cleanly, and groups diagnostics by message so that the largest
/// real gaps rank first instead of the loudest cascade.
/// </summary>
public sealed record VBProjectParityReport(
    VBProject Project,
    ImmutableArray<ParityItemKindSummary> ItemKinds,
    ImmutableArray<ParityFileResult> Files,
    ImmutableArray<ParityGap> Gaps,
    ImmutableArray<ParityDiagnosticCode> DiagnosticCodes,
    ImmutableArray<VBProjectCompilationDiagnostic> ProjectDiagnostics)
{
    /// <summary>Item kinds the compiler currently reads source from.</summary>
    private static readonly ImmutableHashSet<VBProjectItemKind> AnalyzedKinds =
        ImmutableHashSet.Create(VBProjectItemKind.Module);

    public int TotalItemCount => ItemKinds.Sum(kind => kind.Count);

    public int AnalyzedFileCount => Files.Length;

    public int CleanFileCount => Files.Count(file => file.IsClean);

    public int TotalDiagnosticCount => Files.Sum(file => file.DiagnosticCount);

    public static VBProjectParityReport Create(VBProjectCompilationAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var itemKinds = analysis.Project.Items
            .GroupBy(item => item.Kind)
            .Select(group => new ParityItemKindSummary(
                group.Key,
                group.Count(),
                AnalyzedKinds.Contains(group.Key)))
            .OrderByDescending(summary => summary.Count)
            .ThenBy(summary => summary.Kind.ToString(), StringComparer.Ordinal)
            .ToImmutableArray();

        var files = analysis.Units
            .Select(unit =>
            {
                var errors = unit.Analysis.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .ToImmutableArray();

                // Lexer diagnostics are collected before parser diagnostics, so the first entry in
                // the array is not the first problem in the file. The earliest span is what points
                // at the construct that actually derailed the file.
                var firstError = errors
                    .OrderBy(diagnostic => diagnostic.Span.Start)
                    .FirstOrDefault();

                return new ParityFileResult(unit.Item.RelativePath, errors.Length, firstError);
            })
            .OrderByDescending(file => file.DiagnosticCount)
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        var errorDiagnostics = analysis.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        var gaps = errorDiagnostics
            .GroupBy(diagnostic => (diagnostic.Code, diagnostic.Message))
            .Select(group => new ParityGap(
                group.Key.Code,
                group.Key.Message,
                group.Count(),
                group.Select(diagnostic => diagnostic.FilePath ?? string.Empty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()))
            .OrderByDescending(gap => gap.FileCount)
            .ThenByDescending(gap => gap.Occurrences)
            .ThenBy(gap => gap.Message, StringComparer.Ordinal)
            .ToImmutableArray();

        var codes = errorDiagnostics
            .GroupBy(diagnostic => diagnostic.Code)
            .Select(group => new ParityDiagnosticCode(group.Key, group.Count()))
            .OrderByDescending(code => code.Occurrences)
            .ThenBy(code => code.Code, StringComparer.Ordinal)
            .ToImmutableArray();

        return new VBProjectParityReport(
            analysis.Project,
            itemKinds,
            files,
            gaps,
            codes,
            analysis.ProjectDiagnostics);
    }

    public string Render(int gapLimit = 15, int fileLimit = 10)
    {
        var builder = new StringBuilder();
        var projectName = Project.Name ?? Path.GetFileNameWithoutExtension(Project.FilePath);

        builder.AppendLine($"VB6 parity report for {projectName}");
        builder.AppendLine(Project.FilePath);
        builder.AppendLine();

        builder.AppendLine("Project items");
        foreach (var kind in ItemKinds)
        {
            var note = kind.IsAnalyzed ? "analyzed" : "not analyzed yet";
            builder.AppendLine(Row(kind.Kind.ToString(), kind.Count, note));
        }

        builder.AppendLine();
        builder.AppendLine(
            $"Analyzed {AnalyzedFileCount} of {TotalItemCount} project items. " +
            $"{CleanFileCount} of {AnalyzedFileCount} analyze without errors.");

        if (!ProjectDiagnostics.IsDefaultOrEmpty)
        {
            builder.AppendLine();
            builder.AppendLine($"Project file problems ({ProjectDiagnostics.Length})");
            foreach (var diagnostic in ProjectDiagnostics)
            {
                builder.AppendLine($"  {diagnostic.Code}: {diagnostic.Message}");
            }
        }

        if (!Gaps.IsDefaultOrEmpty)
        {
            builder.AppendLine();
            builder.AppendLine($"Largest gaps (by number of files affected, top {Math.Min(gapLimit, Gaps.Length)} of {Gaps.Length})");
            foreach (var gap in Gaps.Take(gapLimit))
            {
                builder.AppendLine(
                    $"  {gap.FileCount,3} files {gap.Occurrences,5}x  {gap.Code}  {gap.Message}");
            }
        }

        if (!DiagnosticCodes.IsDefaultOrEmpty)
        {
            builder.AppendLine();
            builder.AppendLine("Errors by code");
            foreach (var code in DiagnosticCodes)
            {
                builder.AppendLine(Row(code.Code, code.Occurrences, string.Empty));
            }
        }

        var worstFiles = Files.Where(file => !file.IsClean).Take(fileLimit).ToArray();
        if (worstFiles.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Files with the most errors (top {worstFiles.Length})");
            foreach (var file in worstFiles)
            {
                builder.AppendLine($"  {file.DiagnosticCount,5}  {file.RelativePath}");
                if (file.FirstError is not null)
                {
                    builder.AppendLine($"         first: {file.FirstError.Code} {file.FirstError.Message}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine($"Total errors: {TotalDiagnosticCount}");
        return builder.ToString();
    }

    private static string Row(string label, int count, string note)
    {
        var value = count.ToString(CultureInfo.InvariantCulture);
        var suffix = string.IsNullOrEmpty(note) ? string.Empty : $"   {note}";
        return $"  {label,-14} {value,5}{suffix}";
    }
}

public sealed record ParityItemKindSummary(VBProjectItemKind Kind, int Count, bool IsAnalyzed);

public sealed record ParityFileResult(string RelativePath, int DiagnosticCount, Diagnostic? FirstError)
{
    public bool IsClean => DiagnosticCount == 0;
}

public sealed record ParityGap(string Code, string Message, int Occurrences, int FileCount);

public sealed record ParityDiagnosticCode(string Code, int Occurrences);
