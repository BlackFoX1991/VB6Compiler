using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VB6.LanguageServer;

namespace VB6.LanguageServer.Tests;

[TestClass]
public sealed class LspServerTests
{
    [TestMethod]
    public async Task HandlesInitializeAndPublishesCompilerDiagnostics()
    {
        const string uri = "file:///C:/workspace/Module1.bas";
        var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(
            Frame("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"),
            Frame("{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/didOpen\",\"params\":{\"textDocument\":{\"uri\":\"" + uri + "\",\"text\":\"Sub Main()\\n    Debug.Print 1 +\\nEnd Sub\\n\"}}}"),
            Frame("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"shutdown\",\"params\":null}"))));
        var output = new MemoryStream();

        await new LspServer(input, output).RunAsync();

        var text = Encoding.UTF8.GetString(output.ToArray());
        StringAssert.Contains(text, "\"capabilities\"");
        StringAssert.Contains(text, "textDocument/publishDiagnostics");
        StringAssert.Contains(text, "VB6P0001");
    }

    [TestMethod]
    public async Task ServesCompletionDefinitionsAndDocumentSymbols()
    {
        const string uri = "file:///C:/workspace/Module1.bas";
        const string source = "Sub Main()\n" +
                              "    Dim value As Long\n" +
                              "    Dim result As Long\n" +
                              "    result = Compute(value)\n" +
                              "End Sub\n" +
                              "\n" +
                              "Function Compute(ByVal input As Long) As Long\n" +
                              "    Compute = input + 1\n" +
                              "End Function\n";
        var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(
            Frame("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}"),
            Frame("{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/didOpen\",\"params\":{\"textDocument\":{\"uri\":\"" + uri + "\",\"text\":\"" + Escape(source) + "\"}}}"),
            Frame("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"textDocument/completion\",\"params\":{\"textDocument\":{\"uri\":\"" + uri + "\"},\"position\":{\"line\":3,\"character\":17}}}"),
            Frame("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"textDocument/definition\",\"params\":{\"textDocument\":{\"uri\":\"" + uri + "\"},\"position\":{\"line\":3,\"character\":17}}}"),
            Frame("{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"textDocument/documentSymbol\",\"params\":{\"textDocument\":{\"uri\":\"" + uri + "\"}}}"),
            Frame("{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"shutdown\",\"params\":null}"))));
        var output = new MemoryStream();

        await new LspServer(input, output).RunAsync();

        var text = Encoding.UTF8.GetString(output.ToArray());
        StringAssert.Contains(text, "\"id\":2");
        StringAssert.Contains(text, "\"label\":\"Compute\"");
        StringAssert.Contains(text, "\"id\":3");
        StringAssert.Contains(text, "\"uri\":\"" + uri + "\"");
        StringAssert.Contains(text, "\"id\":4");
        StringAssert.Contains(text, "\"name\":\"Main\"");
        StringAssert.Contains(text, "\"name\":\"Compute\"");
    }

    private static string Frame(string json) => $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
