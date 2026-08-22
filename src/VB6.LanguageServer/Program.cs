using VB6.LanguageServer;

await new LspServer(Console.OpenStandardInput(), Console.OpenStandardOutput()).RunAsync();
