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

    private static string Frame(string json) => $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
}
