namespace VB6.Syntax;

public static class SyntaxFacts
{
    public static SyntaxKind GetKeywordKind(string text) => text.ToUpperInvariant() switch
    {
        "OPTION" => SyntaxKind.OptionKeyword,
        "EXPLICIT" => SyntaxKind.ExplicitKeyword,
        "SUB" => SyntaxKind.SubKeyword,
        "FUNCTION" => SyntaxKind.FunctionKeyword,
        "END" => SyntaxKind.EndKeyword,
        "DIM" => SyntaxKind.DimKeyword,
        "AS" => SyntaxKind.AsKeyword,
        "INTEGER" => SyntaxKind.IntegerKeyword,
        "IF" => SyntaxKind.IfKeyword,
        "THEN" => SyntaxKind.ThenKeyword,
        "ELSEIF" => SyntaxKind.ElseIfKeyword,
        "ELSE" => SyntaxKind.ElseKeyword,
        "DEBUG" => SyntaxKind.DebugKeyword,
        "PRINT" => SyntaxKind.PrintKeyword,
        "CALL" => SyntaxKind.CallKeyword,
        "BYREF" => SyntaxKind.ByRefKeyword,
        "BYVAL" => SyntaxKind.ByValKeyword,
        "FOR" => SyntaxKind.ForKeyword,
        "TO" => SyntaxKind.ToKeyword,
        "STEP" => SyntaxKind.StepKeyword,
        "NEXT" => SyntaxKind.NextKeyword,
        "WHILE" => SyntaxKind.WhileKeyword,
        "WEND" => SyntaxKind.WendKeyword,
        "DO" => SyntaxKind.DoKeyword,
        "LOOP" => SyntaxKind.LoopKeyword,
        "UNTIL" => SyntaxKind.UntilKeyword,
        "EXIT" => SyntaxKind.ExitKeyword,
        "SELECT" => SyntaxKind.SelectKeyword,
        "CASE" => SyntaxKind.CaseKeyword,
        "IS" => SyntaxKind.IsKeyword,
        _ => SyntaxKind.IdentifierToken
    };
}
