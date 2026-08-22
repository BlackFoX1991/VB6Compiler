using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VB6.Compiler;
using VB6.Syntax.Diagnostics;
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

            case "textDocument/completion":
            case "textDocument/definition":
            case "textDocument/documentSymbol":
                await ReplyAsync(id, new JsonArray(), cancellationToken);
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

    private static string UriToPath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
        {
            return parsed.LocalPath;
        }

        return uri;
    }

    private sealed record Document(string Uri, string Path, string Text);
}
