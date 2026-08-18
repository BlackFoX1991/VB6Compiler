namespace VB6.Syntax;

public enum SyntaxKind
{
    BadToken,
    EndOfFileToken,
    NewLineToken,

    IdentifierToken,
    IntegerLiteralToken,
    StringLiteralToken,

    PlusToken,
    MinusToken,
    StarToken,
    SlashToken,
    BackslashToken,
    AmpersandToken,
    EqualsToken,
    LessToken,
    LessOrEqualsToken,
    GreaterToken,
    GreaterOrEqualsToken,
    LessGreaterToken,
    OpenParenthesisToken,
    CloseParenthesisToken,
    CommaToken,
    DotToken,
    ColonToken,

    OptionKeyword,
    ExplicitKeyword,
    SubKeyword,
    EndKeyword,
    DimKeyword,
    AsKeyword,
    IntegerKeyword,
    IfKeyword,
    ThenKeyword,
    DebugKeyword,
    PrintKeyword
}
