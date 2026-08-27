using System.Collections.Immutable;
using System.Globalization;

namespace VB6.ProjectSystem;

/// <summary>
/// Parses the text designer envelope used by VB6 forms, user controls, property pages and user
/// documents. The executable VB6 source that follows the envelope is deliberately ignored here;
/// it is parsed by the normal language pipeline after the envelope has been normalized.
/// </summary>
public static class VBDesignerParser
{
    public static VBDesignerParseResult Parse(string source, string filePath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var diagnostics = ImmutableArray.CreateBuilder<VBDesignerDiagnostic>();
        var nodes = new Stack<NodeBuilder>();
        var propertyGroups = new Stack<PropertyGroupFrame>();
        NodeBuilder? root = null;
        var sawDesignerHeader = false;
        var sawDesignerBlock = false;
        var lineNumber = 0;

        using var reader = new StringReader(source);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            var leftTrimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith("'", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("VERSION ", StringComparison.OrdinalIgnoreCase))
            {
                sawDesignerHeader = true;
                continue;
            }

            if (leftTrimmed.StartsWith("BeginProperty", StringComparison.OrdinalIgnoreCase) &&
                (leftTrimmed.Length == "BeginProperty".Length ||
                 char.IsWhiteSpace(leftTrimmed["BeginProperty".Length])))
            {
                if (nodes.Count == 0)
                {
                    diagnostics.Add(new VBDesignerDiagnostic(
                        "VB6FRM0001",
                        "BeginProperty appears outside a designer object.",
                        fullPath,
                        lineNumber));
                    continue;
                }

                var propertyName = leftTrimmed["BeginProperty".Length..].Trim();
                var metadataSeparator = propertyName.IndexOf(' ');
                if (metadataSeparator > 0)
                {
                    propertyName = propertyName[..metadataSeparator];
                }

                if (propertyName.Length == 0)
                {
                    diagnostics.Add(new VBDesignerDiagnostic(
                        "VB6FRM0002",
                        "BeginProperty is missing its property name.",
                        fullPath,
                        lineNumber));
                    continue;
                }

                propertyGroups.Push(new PropertyGroupFrame(nodes.Peek(), propertyName, lineNumber));
                continue;
            }

            if (trimmed.Equals("EndProperty", StringComparison.OrdinalIgnoreCase))
            {
                if (propertyGroups.Count == 0)
                {
                    if (!sawDesignerBlock)
                    {
                        continue;
                    }

                    diagnostics.Add(new VBDesignerDiagnostic(
                        "VB6FRM0003",
                        "EndProperty does not match a BeginProperty block.",
                        fullPath,
                        lineNumber));
                }
                else
                {
                    propertyGroups.Pop();
                }

                continue;
            }

            if (trimmed.StartsWith("Begin ", StringComparison.OrdinalIgnoreCase))
            {
                sawDesignerBlock = true;
                if (!TryParseBegin(trimmed["Begin ".Length..], out var typeName, out var name))
                {
                    diagnostics.Add(new VBDesignerDiagnostic(
                        "VB6FRM0004",
                        "Begin must contain a designer type and object name.",
                        fullPath,
                        lineNumber));
                    continue;
                }

                var builder = new NodeBuilder(typeName, name, lineNumber);
                if (nodes.Count == 0)
                {
                    if (root is not null)
                    {
                        diagnostics.Add(new VBDesignerDiagnostic(
                            "VB6FRM0005",
                            "A designer document can contain only one root object.",
                            fullPath,
                            lineNumber));
                    }
                    else
                    {
                        root = builder;
                    }
                }
                else
                {
                    nodes.Peek().Children.Add(builder);
                }

                nodes.Push(builder);
                continue;
            }

            if (trimmed.Equals("End", StringComparison.OrdinalIgnoreCase))
            {
                if (root is not null && nodes.Count == 0)
                {
                    // The designer envelope is complete. A later standalone "End" can be
                    // ordinary VB6 source (for example an End statement in a procedure).
                    continue;
                }

                if (!sawDesignerBlock && nodes.Count == 0 && propertyGroups.Count == 0)
                {
                    // VB6 .cls metadata uses a standalone BEGIN/END block. It is not a form
                    // designer object and must remain untouched for the source normalizer.
                    continue;
                }

                if (propertyGroups.Count > 0)
                {
                    diagnostics.Add(new VBDesignerDiagnostic(
                        "VB6FRM0006",
                        "End closes a designer object while a BeginProperty block is still open.",
                        fullPath,
                        lineNumber));
                    propertyGroups.Clear();
                }

                if (nodes.Count == 0)
                {
                    diagnostics.Add(new VBDesignerDiagnostic(
                        "VB6FRM0007",
                        "End does not match a Begin block.",
                        fullPath,
                        lineNumber));
                }
                else
                {
                    nodes.Pop();
                }

                continue;
            }

            if (TryParseProperty(trimmed, out var propertyKey, out var rawValue))
            {
                if (nodes.Count == 0)
                {
                    // Attribute/Option lines belong to the source part of the module. They are
                    // not designer properties and must remain available to VBClassModuleSource.
                    continue;
                }

                var resource = ParseResourceReference(StripInlineComment(rawValue), fullPath);
                var resourceData = ReadResourceData(resource, fullPath, lineNumber, diagnostics);
                var propertyName = propertyGroups.Count == 0
                    ? propertyKey
                    : propertyGroups.Peek().Name + "." + propertyKey;
                nodes.Peek().Properties.Add(
                    new VBDesignerProperty(
                        propertyName,
                        rawValue,
                        ParseValue(rawValue),
                        lineNumber,
                        resource.Path,
                        resource.Offset)
                    {
                        ResourceData = resourceData
                    });
            }
        }

        if (propertyGroups.Count > 0)
        {
            foreach (var group in propertyGroups)
            {
                diagnostics.Add(new VBDesignerDiagnostic(
                    "VB6FRM0008",
                    $"BeginProperty '{group.Name}' is not closed.",
                    fullPath,
                    group.Line));
            }
        }

        if (nodes.Count > 0)
        {
            foreach (var node in nodes)
            {
                diagnostics.Add(new VBDesignerDiagnostic(
                    "VB6FRM0009",
                    $"Designer object '{node.Name}' is not closed.",
                    fullPath,
                    node.Line));
            }
        }

        if (sawDesignerHeader && sawDesignerBlock && root is null)
        {
            diagnostics.Add(new VBDesignerDiagnostic(
                "VB6FRM0010",
                "The designer header does not contain a root object.",
                fullPath));
        }

        var document = root is null
            ? null
            : new VBDesignerDocument(fullPath, root.Build());
        return new VBDesignerParseResult(document, diagnostics.ToImmutable());
    }

    public static VBDesignerParseResult ParseFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        return Parse(VB6TextFile.ReadAllText(fullPath), fullPath);
    }

    private static bool TryParseBegin(string text, out string typeName, out string name)
    {
        var separator = text.IndexOfAny(new[] { ' ', '\t' });
        if (separator <= 0)
        {
            typeName = string.Empty;
            name = string.Empty;
            return false;
        }

        typeName = text[..separator].Trim();
        name = text[separator..].Trim();
        if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
        {
            name = name[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        return typeName.Length != 0 && name.Length != 0;
    }

    private static bool TryParseProperty(string text, out string key, out string value)
    {
        var separator = FindAssignmentSeparator(text);
        if (separator <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = text[..separator].Trim();
        value = text[(separator + 1)..].Trim();
        return key.Length != 0;
    }

    private static int FindAssignmentSeparator(string text)
    {
        var quoted = false;
        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '"':
                    if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                    break;
                case '=' when !quoted:
                    return index;
            }
        }

        return -1;
    }

    private static object? ParseValue(string rawValue)
    {
        rawValue = StripInlineComment(rawValue);
        if (rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[^1] == '"')
        {
            return rawValue[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        if (rawValue.Equals("True", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (rawValue.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rawValue.Equals("Nothing", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (rawValue.StartsWith("&H", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(rawValue[2..].TrimEnd('&'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
        {
            return hex;
        }

        if (long.TryParse(rawValue.TrimEnd('&'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(rawValue.TrimEnd('!', '#', '@'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return rawValue;
    }

    private static string StripInlineComment(string rawValue)
    {
        var quoted = false;
        for (var index = 0; index < rawValue.Length; index++)
        {
            if (rawValue[index] != '"')
            {
                if (rawValue[index] == '\'' && !quoted)
                {
                    return rawValue[..index].TrimEnd();
                }

                continue;
            }

            if (quoted && index + 1 < rawValue.Length && rawValue[index + 1] == '"')
            {
                index++;
            }
            else
            {
                quoted = !quoted;
            }
        }

        return rawValue.Trim();
    }

    private static (string? Path, int? Offset) ParseResourceReference(string rawValue, string sourcePath)
    {
        var separator = rawValue.IndexOf(':');
        var quoteStart = rawValue.StartsWith("$\"", StringComparison.Ordinal) ? 1 : 0;
        if (separator <= quoteStart || rawValue.Length <= quoteStart || rawValue[quoteStart] != '"')
        {
            return (null, null);
        }

        var quote = rawValue.IndexOf('"', quoteStart + 1);
        if (quote <= 1 || !int.TryParse(rawValue[(separator + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
        {
            return (null, null);
        }

        var relativePath = rawValue[(quoteStart + 1)..quote];
        return (Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath) ?? string.Empty, relativePath)), offset);
    }

    private static byte[]? ReadResourceData(
        (string? Path, int? Offset) resource,
        string sourcePath,
        int lineNumber,
        ImmutableArray<VBDesignerDiagnostic>.Builder diagnostics)
    {
        if (resource.Path is null || resource.Offset is not int offset || !File.Exists(resource.Path))
        {
            return null;
        }

        try
        {
            return VBFrxResourceReader.Read(resource.Path, offset);
        }
        catch (InvalidDataException exception)
        {
            diagnostics.Add(new VBDesignerDiagnostic(
                "VB6FRX0001",
                exception.Message,
                sourcePath,
                lineNumber,
                VBDesignerDiagnosticSeverity.Warning));
            return null;
        }
    }

    private sealed class NodeBuilder
    {
        public NodeBuilder(string typeName, string name, int line)
        {
            TypeName = typeName;
            Name = name;
            Line = line;
        }

        public string TypeName { get; }
        public string Name { get; }
        public int Line { get; }
        public List<VBDesignerProperty> Properties { get; } = new();
        public List<NodeBuilder> Children { get; } = new();

        public VBDesignerNode Build() => new(
            TypeName,
            Name,
            Line,
            Properties.ToImmutableArray(),
            Children.Select(child => child.Build()).ToImmutableArray());
    }

    private sealed record PropertyGroupFrame(NodeBuilder Node, string Name, int Line);
}

public sealed record VBDesignerParseResult(
    VBDesignerDocument? Document,
    ImmutableArray<VBDesignerDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != VBDesignerDiagnosticSeverity.Error);
}

public sealed record VBDesignerDocument(string FilePath, VBDesignerNode Root);

public sealed record VBDesignerNode(
    string TypeName,
    string Name,
    int Line,
    ImmutableArray<VBDesignerProperty> Properties,
    ImmutableArray<VBDesignerNode> Children)
{
    public bool IsControlArray =>
        Properties.Any(property =>
            property.Name.Equals("Index", StringComparison.OrdinalIgnoreCase) &&
            property.Value is long);

    public int? ArrayIndex => Properties
        .FirstOrDefault(property => property.Name.Equals("Index", StringComparison.OrdinalIgnoreCase))
        ?.Value switch
    {
        long value when value is >= int.MinValue and <= int.MaxValue => (int)value,
        int value => value,
        _ => null
    };

    public IEnumerable<VBDesignerNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
        {
            foreach (var descendant in child.DescendantsAndSelf())
            {
                yield return descendant;
            }
        }
    }
}

public sealed record VBDesignerProperty(
    string Name,
    string RawValue,
    object? Value,
    int Line,
    string? ResourcePath,
    int? ResourceOffset)
{
    /// <summary>Opaque bytes decoded from the referenced .frx resource, when the file is present.</summary>
    public byte[]? ResourceData { get; init; }
}

public enum VBDesignerDiagnosticSeverity
{
    Error,
    Warning
}

public sealed record VBDesignerDiagnostic(
    string Code,
    string Message,
    string? FilePath = null,
    int? Line = null,
    VBDesignerDiagnosticSeverity Severity = VBDesignerDiagnosticSeverity.Error)
{
    public override string ToString()
    {
        var location = FilePath is null
            ? string.Empty
            : Line is null
                ? $"{FilePath}: "
                : $"{FilePath}({Line}): ";
        return $"{location}{Code}: {Message}";
    }
}
