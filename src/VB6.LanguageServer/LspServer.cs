using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VB6.Compiler;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.LanguageServer;

/// <summary>Small dependency-free JSON-RPC/LSP host for compiler diagnostics and navigation.</summary>
public sealed class LspServer
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly ConcurrentDictionary<string, Document> _documents = new(StringComparer.Ordinal);

    public LspServer(Stream input, Stream output)
    {
        _input = input;
        _output = output;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (await ReadMessageAsync(cancellationToken) is { } message)
        {
            using (message)
            {
                await HandleAsync(message.RootElement, cancellationToken);
            }
        }
    }

    private async Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        var method = message.TryGetProperty("method", out var methodValue)
            ? methodValue.GetString()
            : null;
        if (method is null)
        {
            return;
        }

        message.TryGetProperty("id", out var id);
        switch (method)
        {
            case "initialize":
                await ReplyAsync(id, new JsonObject
                {
                    ["capabilities"] = new JsonObject
                    {
                        ["textDocumentSync"] = 1,
                        ["completionProvider"] = new JsonObject(),
                        ["definitionProvider"] = true,
                        ["documentSymbolProvider"] = true
                    }
                }, cancellationToken);
                break;

            case "shutdown":
                await ReplyAsync(id, null, cancellationToken);
                break;

            case "textDocument/didOpen":
                UpdateDocument(message, "textDocument");
                await PublishDiagnosticsAsync(message, cancellationToken);
                break;

            case "textDocument/didChange":
                UpdateDocument(message, "textDocument", "contentChanges");
                await PublishDiagnosticsAsync(message, cancellationToken);
                break;

            case "textDocument/didClose":
                CloseDocument(message);
                break;

            case "textDocument/completion":
                await ReplyAsync(id, Completion(message), cancellationToken);
                break;

            case "textDocument/definition":
                await ReplyAsync(id, Definitions(message), cancellationToken);
                break;

            case "textDocument/documentSymbol":
                await ReplyAsync(id, DocumentSymbols(message), cancellationToken);
                break;
        }
    }

    private void UpdateDocument(JsonElement message, string documentProperty, string? changesProperty = null)
    {
        if (!message.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty(documentProperty, out var document))
        {
            return;
        }

        var uri = document.GetProperty("uri").GetString() ?? string.Empty;
        var text = document.TryGetProperty("text", out var openedText)
            ? openedText.GetString() ?? string.Empty
            : changesProperty is not null &&
              parameters.TryGetProperty(changesProperty, out var changes) &&
              changes.GetArrayLength() > 0
                ? changes[0].GetProperty("text").GetString() ?? string.Empty
                : _documents.TryGetValue(uri, out var existing) ? existing.Text : string.Empty;
        _documents[uri] = new Document(uri, UriToPath(uri), text);
    }

    private void CloseDocument(JsonElement message)
    {
        if (!message.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty("textDocument", out var textDocument) ||
            !textDocument.TryGetProperty("uri", out var uriValue))
        {
            return;
        }

        var uri = uriValue.GetString() ?? string.Empty;
        _documents.TryRemove(uri, out _);
    }

    private JsonArray Completion(JsonElement message)
    {
        if (!TryGetRequestDocument(message, out var document, out var parameters))
        {
            return new JsonArray();
        }

        var source = SourceText.From(document.Text, document.Path);
        var root = VBCompilation.Create(document.Text, document.Path).Analyze().ParseResult.Root;
        var prefix = GetWordPrefix(source, parameters);
        var candidates = GetDeclarations(root)
            .Select(declaration => (
                Name: declaration.Name,
                Kind: declaration.Kind,
                Detail: declaration.Detail))
            .Concat(CommonIntrinsicNames.Select(name => (
                Name: name,
                Kind: SymbolKind.Function,
                Detail: "VB6 intrinsic")))
            .Where(candidate => prefix.Length == 0 ||
                candidate.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase);

        var result = new JsonArray();
        foreach (var candidate in candidates)
        {
            result.Add(new JsonObject
            {
                ["label"] = candidate.Name,
                ["kind"] = candidate.Kind,
                ["detail"] = candidate.Detail
            });
        }

        return result;
    }

    private JsonArray Definitions(JsonElement message)
    {
        if (!TryGetRequestDocument(message, out var document, out var parameters))
        {
            return new JsonArray();
        }

        var source = SourceText.From(document.Text, document.Path);
        var root = VBCompilation.Create(document.Text, document.Path).Analyze().ParseResult.Root;
        var word = GetWordAtPosition(source, parameters);
        var result = new JsonArray();
        foreach (var declaration in GetDeclarations(root).Where(declaration =>
                     string.Equals(declaration.Name, word, StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(new JsonObject
            {
                ["uri"] = document.Uri,
                ["range"] = Range(source.GetLinePositionSpan(declaration.Span))
            });
        }

        return result;
    }

    private JsonArray DocumentSymbols(JsonElement message)
    {
        if (!TryGetRequestDocument(message, out var document, out _))
        {
            return new JsonArray();
        }

        var source = SourceText.From(document.Text, document.Path);
        var root = VBCompilation.Create(document.Text, document.Path).Analyze().ParseResult.Root;
        var result = new JsonArray();
        foreach (var declaration in GetDeclarations(root))
        {
            var range = Range(source.GetLinePositionSpan(declaration.Span));
            result.Add(new JsonObject
            {
                ["name"] = declaration.Name,
                ["kind"] = declaration.Kind,
                ["detail"] = declaration.Detail,
                ["range"] = range,
                ["selectionRange"] = range.DeepClone()
            });
        }

        return result;
    }

    private bool TryGetRequestDocument(
        JsonElement message,
        out Document document,
        out JsonElement parameters)
    {
        document = null!;
        parameters = default;
        if (!message.TryGetProperty("params", out parameters) ||
            !parameters.TryGetProperty("textDocument", out var textDocument) ||
            !textDocument.TryGetProperty("uri", out var uriValue))
        {
            return false;
        }

        var uri = uriValue.GetString() ?? string.Empty;
        return _documents.TryGetValue(uri, out document!);
    }

    private static string GetWordPrefix(SourceText source, JsonElement parameters)
    {
        var position = ReadPosition(parameters);
        var line = GetLineText(source, position.Line);
        var character = Math.Clamp(position.Character, 0, line.Length);
        var start = character;
        while (start > 0 && IsIdentifierPart(line[start - 1]))
        {
            start--;
        }

        return line[start..character];
    }

    private static string GetWordAtPosition(SourceText source, JsonElement parameters)
    {
        var position = ReadPosition(parameters);
        var line = GetLineText(source, position.Line);
        var character = Math.Clamp(position.Character, 0, line.Length);
        var start = character;
        var end = character;
        while (start > 0 && IsIdentifierPart(line[start - 1]))
        {
            start--;
        }

        while (end < line.Length && IsIdentifierPart(line[end]))
        {
            end++;
        }

        return line[start..end];
    }

    private static LspPosition ReadPosition(JsonElement parameters)
    {
        if (!parameters.TryGetProperty("position", out var position))
        {
            return new LspPosition(0, 0);
        }

        return new LspPosition(
            position.TryGetProperty("line", out var line) ? line.GetInt32() : 0,
            position.TryGetProperty("character", out var character) ? character.GetInt32() : 0);
    }

    private static string GetLineText(SourceText source, int line)
    {
        if (line < 0 || line >= source.Lines.Length)
        {
            return string.Empty;
        }

        return source.ToString(source.Lines[line].Span);
    }

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_' || "$%&!#@".Contains(value);

    private static IEnumerable<DeclarationInfo> GetDeclarations(CompilationUnitSyntax root)
    {
        foreach (var member in root.Members)
        {
            switch (member)
            {
                case SubDeclarationSyntax sub:
                    yield return Declaration(sub.Identifier, SymbolKind.Method, "Sub");
                    break;
                case FunctionDeclarationSyntax function:
                    yield return Declaration(function.Identifier, SymbolKind.Function, "Function");
                    break;
                case PropertyDeclarationSyntax property:
                    yield return Declaration(property.Identifier, SymbolKind.Property, "Property " + property.AccessorKeyword.Text);
                    break;
                case EventDeclarationSyntax @event:
                    yield return Declaration(@event.Identifier, SymbolKind.Event, "Event");
                    break;
                case DeclareDeclarationSyntax declare:
                    yield return Declaration(declare.Identifier, SymbolKind.Function, "Declare");
                    break;
                case EnumDeclarationSyntax @enum:
                    yield return Declaration(@enum.Identifier, SymbolKind.Enum, "Enum");
                    foreach (var memberSymbol in @enum.Members)
                    {
                        yield return Declaration(memberSymbol.Identifier, SymbolKind.EnumMember, "Enum member");
                    }

                    break;
                case TypeDeclarationSyntax type:
                    yield return Declaration(type.Identifier, SymbolKind.Struct, "Type");
                    break;
                case ConstDeclarationSyntax constant:
                    yield return Declaration(constant.Identifier, SymbolKind.Constant, "Const");
                    break;
                case ModuleVariableDeclarationSyntax variables:
                    foreach (var variable in variables.Declarators)
                    {
                        yield return Declaration(variable.Identifier, SymbolKind.Variable, "Variable");
                    }

                    break;
            }
        }
    }

    private static DeclarationInfo Declaration(SyntaxToken token, int kind, string detail) =>
        new(token.Text, token.Span, kind, detail);

    private static readonly string[] CommonIntrinsicNames =
    {
        "Abs", "Asc", "CByte", "CDate", "CDec", "CDbl", "CInt", "CLng", "CStr", "CSng",
        "Date", "DateAdd", "DateDiff", "DatePart", "Debug", "DoEvents", "Format", "Hex",
        "IIf", "InStr", "Left", "Len", "Like", "LCase", "Mid", "MsgBox", "RGB", "Right",
        "Round", "Sgn", "Sin", "Sqr", "String", "Time", "Trim", "UCase", "Val"
    };

    private async Task PublishDiagnosticsAsync(JsonElement message, CancellationToken cancellationToken)
    {
        if (!message.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty("textDocument", out var textDocument))
        {
            return;
        }

        var uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
        if (!_documents.TryGetValue(uri, out var document))
        {
            return;
        }

        var source = SourceText.From(document.Text, document.Path);
        var analysis = VBCompilation.Create(document.Text, document.Path).Analyze();
        var diagnostics = new JsonArray();
        foreach (var diagnostic in analysis.Diagnostics.Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Info))
        {
            var lines = source.GetLinePositionSpan(diagnostic.Span);
            diagnostics.Add(new JsonObject
            {
                ["range"] = new JsonObject
                {
                    ["start"] = Position(lines.Start),
                    ["end"] = Position(lines.End)
                },
                ["severity"] = diagnostic.Severity == DiagnosticSeverity.Error ? 1 : 2,
                ["code"] = diagnostic.Code,
                ["source"] = "vb6c",
                ["message"] = diagnostic.Message
            });
        }

        await WriteMessageAsync(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "textDocument/publishDiagnostics",
            ["params"] = new JsonObject
            {
                ["uri"] = uri,
                ["diagnostics"] = diagnostics
            }
        }, cancellationToken);
    }

    private async Task ReplyAsync(JsonElement id, JsonNode? result, CancellationToken cancellationToken)
    {
        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.ValueKind == JsonValueKind.Undefined ? null : JsonNode.Parse(id.GetRawText())
        };
        response["result"] = result;
        await WriteMessageAsync(response, cancellationToken);
    }

    private async Task WriteMessageAsync(JsonNode message, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(message.ToJsonString() + "\r\n");
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
        await _output.WriteAsync(header, cancellationToken);
        await _output.WriteAsync(payload, cancellationToken);
        await _output.FlushAsync(cancellationToken);
    }

    private async Task<JsonDocument?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        using var header = new MemoryStream();
        var terminator = new byte[4];
        while (true)
        {
            var value = await ReadByteAsync(cancellationToken);
            if (value < 0)
            {
                return null;
            }

            header.WriteByte((byte)value);
            terminator[0] = terminator[1];
            terminator[1] = terminator[2];
            terminator[2] = terminator[3];
            terminator[3] = (byte)value;
            if (terminator.SequenceEqual(new byte[] { 13, 10, 13, 10 }))
            {
                break;
            }
        }

        var headerText = Encoding.ASCII.GetString(header.ToArray());
        var lengthLine = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        if (lengthLine is null || !int.TryParse(lengthLine[15..].Trim(), out var length))
        {
            return null;
        }

        var body = new byte[length];
        var read = 0;
        while (read < body.Length)
        {
            var count = await _input.ReadAsync(body.AsMemory(read), cancellationToken);
            if (count == 0)
            {
                return null;
            }

            read += count;
        }

        return JsonDocument.Parse(body);
    }

    private async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var read = await _input.ReadAsync(buffer, cancellationToken);
        return read == 0 ? -1 : buffer[0];
    }

    private static JsonObject Position(LinePosition position) => new()
    {
        ["line"] = position.Line,
        ["character"] = position.Character
    };

    private static JsonObject Range(LinePositionSpan span) => new()
    {
        ["start"] = Position(span.Start),
        ["end"] = Position(span.End)
    };

    private static string UriToPath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
        {
            return parsed.LocalPath;
        }

        return uri;
    }

    private sealed record Document(string Uri, string Path, string Text);
    private sealed record DeclarationInfo(string Name, TextSpan Span, int Kind, string Detail);
    private readonly record struct LspPosition(int Line, int Character);

    private static class SymbolKind
    {
        public const int Method = 6;
        public const int Property = 7;
        public const int Variable = 13;
        public const int Constant = 14;
        public const int Function = 12;
        public const int Enum = 10;
        public const int EnumMember = 22;
        public const int Struct = 23;
        public const int Event = 24;
    }
}
