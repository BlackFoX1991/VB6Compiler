using System.Text;

namespace VB6.Compiler;

/// <summary>
/// Removes the designer metadata envelope from VB6 class, form, user-control, property-page and
/// user-document modules while
/// preserving every source offset and line ending. The declarations after the Attribute block are
/// ordinary VB6 source and go through the same lexer/parser pipeline as a standard module.
/// </summary>
internal static class VBClassModuleSource
{
    public static string Normalize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!LooksLikeDesignerModule(source))
        {
            return source;
        }

        var builder = new StringBuilder(source.Length);
        var position = 0;
        var attributesStarted = false;
        while (position < source.Length)
        {
            var lineStart = position;
            while (position < source.Length && source[position] is not ('\r' or '\n'))
            {
                position++;
            }

            var line = source[lineStart..position];
            var trimmed = line.Trim();
            if (!attributesStarted && trimmed.StartsWith("Attribute ", StringComparison.OrdinalIgnoreCase))
            {
                attributesStarted = true;
            }

            if (IsSemanticAttribute(trimmed))
            {
                builder.Append(line);
            }
            else if (attributesStarted && !trimmed.StartsWith("Attribute ", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(line);
            }
            else
            {
                // Keep offsets stable for diagnostics and source mapping. The parser sees a blank
                // line, while the original class metadata remains available to project tooling.
                builder.Append(' ', line.Length);
            }

            if (position < source.Length)
            {
                if (source[position] == '\r')
                {
                    builder.Append('\r');
                    position++;
                    if (position < source.Length && source[position] == '\n')
                    {
                        builder.Append('\n');
                        position++;
                    }
                }
                else
                {
                    builder.Append('\n');
                    position++;
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The attribute lines that carry meaning for the compiler rather than for the IDE. Everything
    /// else is blanked out so the parser never sees it -- but these two change what the code means,
    /// so they have to survive: VB_UserMemId names the default property, and VB_PredeclaredId gives
    /// the class a global instance named after itself.
    /// </summary>
    private static bool IsSemanticAttribute(string line) =>
        IsDefaultPropertyAttribute(line) ||
        IsPredeclaredIdAttribute(line) ||
        IsInstancingAttribute(line);

    private static bool IsDefaultPropertyAttribute(string line) =>
        line.StartsWith("Attribute ", StringComparison.OrdinalIgnoreCase) &&
        line.Contains(".VB_UserMemId", StringComparison.OrdinalIgnoreCase) &&
        line.Contains("= 0", StringComparison.OrdinalIgnoreCase);

    private static bool IsPredeclaredIdAttribute(string line) =>
        line.StartsWith("Attribute ", StringComparison.OrdinalIgnoreCase) &&
        line.Contains("VB_PredeclaredId", StringComparison.OrdinalIgnoreCase);

    /// <summary>VB_Exposed and VB_Creatable are how VB6 writes down a class Instancing.</summary>
    private static bool IsInstancingAttribute(string line) =>
        line.StartsWith("Attribute ", StringComparison.OrdinalIgnoreCase) &&
        (line.Contains("VB_Exposed", StringComparison.OrdinalIgnoreCase) ||
         line.Contains("VB_Creatable", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeDesignerModule(string source)
    {
        var sawVersionHeader = false;
        using var reader = new StringReader(source);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("'", StringComparison.Ordinal))
            {
                continue;
            }

            if (!trimmed.StartsWith("VERSION ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            sawVersionHeader = true;
            break;
        }

        if (!sawVersionHeader)
        {
            return false;
        }

        using var designerReader = new StringReader(source);
        while ((line = designerReader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("Begin ", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("BeginProperty ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return source.Contains(" CLASS", StringComparison.OrdinalIgnoreCase);
    }
}
