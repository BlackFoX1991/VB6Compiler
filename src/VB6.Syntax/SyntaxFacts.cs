namespace VB6.Syntax;

public static class SyntaxFacts
{
    public static SyntaxKind GetKeywordKind(string text) => text.ToUpperInvariant() switch
    {
        "OPTION" => SyntaxKind.OptionKeyword,
        "EXPLICIT" => SyntaxKind.ExplicitKeyword,
        "SUB" => SyntaxKind.SubKeyword,
        "FUNCTION" => SyntaxKind.FunctionKeyword,
        "PROPERTY" => SyntaxKind.PropertyKeyword,
        "EVENT" => SyntaxKind.EventKeyword,
        "RAISEEVENT" => SyntaxKind.RaiseEventKeyword,
        "DECLARE" => SyntaxKind.DeclareKeyword,
        "LIB" => SyntaxKind.LibKeyword,
        "ALIAS" => SyntaxKind.AliasKeyword,
        "ENUM" => SyntaxKind.EnumKeyword,
        "TYPE" => SyntaxKind.TypeKeyword,
        "END" => SyntaxKind.EndKeyword,
        "DIM" => SyntaxKind.DimKeyword,
        "STATIC" => SyntaxKind.StaticKeyword,
        "CONST" => SyntaxKind.ConstKeyword,
        "AS" => SyntaxKind.AsKeyword,
        "BYTE" => SyntaxKind.ByteKeyword,
        "INTEGER" => SyntaxKind.IntegerKeyword,
        "LONG" => SyntaxKind.LongKeyword,
        "SINGLE" => SyntaxKind.SingleKeyword,
        "DOUBLE" => SyntaxKind.DoubleKeyword,
        "DECIMAL" => SyntaxKind.DecimalKeyword,
        "IF" => SyntaxKind.IfKeyword,
        "THEN" => SyntaxKind.ThenKeyword,
        "ELSEIF" => SyntaxKind.ElseIfKeyword,
        "ELSE" => SyntaxKind.ElseKeyword,
        "TRUE" => SyntaxKind.TrueKeyword,
        "FALSE" => SyntaxKind.FalseKeyword,
        "EMPTY" => SyntaxKind.EmptyKeyword,
        "NULL" => SyntaxKind.NullKeyword,
        "NOTHING" => SyntaxKind.NothingKeyword,
        "NOT" => SyntaxKind.NotKeyword,
        "AND" => SyntaxKind.AndKeyword,
        "OR" => SyntaxKind.OrKeyword,
        "XOR" => SyntaxKind.XorKeyword,
        "EQV" => SyntaxKind.EqvKeyword,
        "IMP" => SyntaxKind.ImpKeyword,
        "MOD" => SyntaxKind.ModKeyword,
        "LIKE" => SyntaxKind.LikeKeyword,
        "DEBUG" => SyntaxKind.DebugKeyword,
        "PRINT" => SyntaxKind.PrintKeyword,
        "CALL" => SyntaxKind.CallKeyword,
        "OPTIONAL" => SyntaxKind.OptionalKeyword,
        "PARAMARRAY" => SyntaxKind.ParamArrayKeyword,
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
        "WITH" => SyntaxKind.WithKeyword,
        "EXIT" => SyntaxKind.ExitKeyword,
        "SELECT" => SyntaxKind.SelectKeyword,
        "CASE" => SyntaxKind.CaseKeyword,
        "IS" => SyntaxKind.IsKeyword,
        _ => SyntaxKind.IdentifierToken
    };

    /// <summary>
    /// Whether the lexer turned this token into a keyword purely because of its spelling.
    ///
    /// VB6 reserves these words in statement position but still allows them as declaration
    /// names in places where the grammar cannot be ambiguous — a user-defined type may declare
    /// <c>Property As Boolean</c> or <c>Alias As String</c>. The parser uses this to accept such
    /// a token where it expects a name, instead of rejecting real legacy code.
    /// </summary>
    public static bool IsKeywordToken(SyntaxToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.Kind != SyntaxKind.IdentifierToken &&
            token.Text.Length > 0 &&
            GetKeywordKind(token.Text) == token.Kind;
    }
}
