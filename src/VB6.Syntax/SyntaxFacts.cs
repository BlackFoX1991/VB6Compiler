namespace VB6.Syntax;

public static class SyntaxFacts
{
    public static SyntaxKind GetKeywordKind(string text) => text.ToUpperInvariant() switch
    {
        "OPTION" => SyntaxKind.OptionKeyword,
        "EXPLICIT" => SyntaxKind.ExplicitKeyword,
        "SUB" => SyntaxKind.SubKeyword,
        "END" => SyntaxKind.EndKeyword,
        "DIM" => SyntaxKind.DimKeyword,
        "AS" => SyntaxKind.AsKeyword,
        "INTEGER" => SyntaxKind.IntegerKeyword,
        "IF" => SyntaxKind.IfKeyword,
        "THEN" => SyntaxKind.ThenKeyword,
        "DEBUG" => SyntaxKind.DebugKeyword,
        "PRINT" => SyntaxKind.PrintKeyword,
        _ => SyntaxKind.IdentifierToken
    };
}
