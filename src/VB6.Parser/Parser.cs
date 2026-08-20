using System.Collections.Immutable;
using VB6.Lexer;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Parser;

public sealed class Parser
{
    private readonly SourceText _text;
    private readonly ImmutableArray<SyntaxToken> _tokens;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    private int _position;

    public Parser(SourceText text)
    {
        _text = text;
        var lexResult = new LexerType(text).Lex();
        _tokens = lexResult.Tokens.Where(token => token.Kind != SyntaxKind.BadToken).ToImmutableArray();
        _diagnostics.AddRange(lexResult.Diagnostics);
    }

    public ParseResult ParseCompilationUnit()
    {
        var members = ImmutableArray.CreateBuilder<MemberSyntax>();
        SkipNewLines();

        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            var start = _position;
            var member = ParseMember();
            if (member is not null)
            {
                members.Add(member);
            }

            if (_position == start)
            {
                ReportUnexpected(Current, "module member");
                NextToken();
            }

            SkipNewLines();
        }

        return new ParseResult(
            new CompilationUnitSyntax(members.ToImmutable(), MatchToken(SyntaxKind.EndOfFileToken)),
            _diagnostics.ToImmutable());
    }

    private MemberSyntax? ParseMember()
    {
        if (IsAttributeLine())
        {
            return ParseAttribute();
        }

        if (Current.Kind == SyntaxKind.OptionKeyword && Peek(1).Kind == SyntaxKind.ExplicitKeyword)
        {
            return ParseOptionExplicit();
        }

        if (IsOptionDirective("Base"))
        {
            return ParseOptionBase();
        }

        if (IsOptionDirective("Compare"))
        {
            return ParseOptionCompare();
        }

        if (Current.Kind == SyntaxKind.DeclareKeyword)
        {
            return ParseDeclareDeclaration(null);
        }

        if (IsVisibilityModifier(Current) && Peek(1).Kind == SyntaxKind.DeclareKeyword)
        {
            return ParseDeclareDeclaration(NextToken());
        }

        if (Current.Kind == SyntaxKind.EnumKeyword)
        {
            return ParseEnumDeclaration(null);
        }

        if (IsVisibilityModifier(Current) && Peek(1).Kind == SyntaxKind.EnumKeyword)
        {
            return ParseEnumDeclaration(NextToken());
        }

        if (Current.Kind == SyntaxKind.TypeKeyword)
        {
            return ParseTypeDeclaration(null);
        }

        if (IsVisibilityModifier(Current) && Peek(1).Kind == SyntaxKind.TypeKeyword)
        {
            return ParseTypeDeclaration(NextToken());
        }

        if (IsVisibilityModifier(Current) && Peek(1).Kind is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword)
        {
            var visibility = NextToken();
            return Current.Kind == SyntaxKind.SubKeyword
                ? ParseSubDeclaration(visibility)
                : ParseFunctionDeclaration(visibility);
        }

        if (LooksLikeModuleVariableDeclaration())
        {
            return ParseModuleVariableDeclaration(NextToken());
        }

        if (Current.Kind == SyntaxKind.ConstKeyword)
        {
            return ParseConstDeclaration(null);
        }

        if (IsVisibilityModifier(Current) && Peek(1).Kind == SyntaxKind.ConstKeyword)
        {
            return ParseConstDeclaration(NextToken());
        }

        if (Current.Kind == SyntaxKind.SubKeyword)
        {
            return ParseSubDeclaration();
        }

        if (Current.Kind == SyntaxKind.FunctionKeyword)
        {
            return ParseFunctionDeclaration();
        }

        return null;
    }

    private DeclareDeclarationSyntax ParseDeclareDeclaration(SyntaxToken? visibilityKeyword)
    {
        var declareKeyword = MatchToken(SyntaxKind.DeclareKeyword);
        SyntaxToken procedureKindKeyword;
        if (Current.Kind is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword)
        {
            procedureKindKeyword = NextToken();
        }
        else
        {
            procedureKindKeyword = MatchToken(SyntaxKind.FunctionKeyword);
        }

        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var libKeyword = MatchToken(SyntaxKind.LibKeyword);
        var libraryName = MatchToken(SyntaxKind.StringLiteralToken);

        SyntaxToken? aliasKeyword = null;
        SyntaxToken? aliasName = null;
        if (Current.Kind == SyntaxKind.AliasKeyword)
        {
            aliasKeyword = NextToken();
            aliasName = MatchToken(SyntaxKind.StringLiteralToken);
        }

        var openParenthesis = MatchToken(SyntaxKind.OpenParenthesisToken);
        var parameters = ParseParameters();
        var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);

        SyntaxToken? asKeyword = null;
        SyntaxToken? returnType = null;
        if (procedureKindKeyword.Kind == SyntaxKind.FunctionKeyword && Current.Kind == SyntaxKind.AsKeyword)
        {
            asKeyword = NextToken();
            returnType = MatchTypeToken();
        }

        ConsumeLineTerminator();
        return new DeclareDeclarationSyntax(
            visibilityKeyword,
            declareKeyword,
            procedureKindKeyword,
            identifier,
            libKeyword,
            libraryName,
            aliasKeyword,
            aliasName,
            openParenthesis,
            parameters,
            closeParenthesis,
            asKeyword,
            returnType);
    }

    private EnumDeclarationSyntax ParseEnumDeclaration(SyntaxToken? visibilityKeyword)
    {
        var enumKeyword = MatchToken(SyntaxKind.EnumKeyword);
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        ConsumeLineTerminator();

        var members = ImmutableArray.CreateBuilder<EnumMemberSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken && !IsEndPair(SyntaxKind.EnumKeyword))
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            var memberIdentifier = MatchToken(SyntaxKind.IdentifierToken);
            SyntaxToken? equalsToken = null;
            ExpressionSyntax? value = null;
            if (Current.Kind == SyntaxKind.EqualsToken)
            {
                equalsToken = NextToken();
                value = ParseExpression();
            }

            ConsumeLineTerminator();
            members.Add(new EnumMemberSyntax(memberIdentifier, equalsToken, value));
        }

        var endKeyword = MatchToken(SyntaxKind.EndKeyword);
        var endEnumKeyword = MatchToken(SyntaxKind.EnumKeyword);
        ConsumeLineTerminator();
        return new EnumDeclarationSyntax(
            visibilityKeyword,
            enumKeyword,
            identifier,
            members.ToImmutable(),
            endKeyword,
            endEnumKeyword);
    }

    private TypeDeclarationSyntax ParseTypeDeclaration(SyntaxToken? visibilityKeyword)
    {
        var typeKeyword = MatchToken(SyntaxKind.TypeKeyword);
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        ConsumeLineTerminator();

        var members = ImmutableArray.CreateBuilder<TypeMemberSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken && !IsEndPair(SyntaxKind.TypeKeyword))
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            var memberStart = _position;
            var memberIdentifier = MatchTypeMemberName();
            var (openParenthesis, dimensions, closeParenthesis) = ParseArrayDimensions();
            var asKeyword = MatchToken(SyntaxKind.AsKeyword);
            var memberType = MatchTypeToken();

            SyntaxToken? starToken = null;
            ExpressionSyntax? fixedStringLength = null;
            if (Current.Kind == SyntaxKind.StarToken)
            {
                starToken = NextToken();
                fixedStringLength = ParseExpression();
            }

            ConsumeLineTerminator();
            if (_position == memberStart)
            {
                RecoverToLineEndOrTypeEnd();
                continue;
            }

            members.Add(new TypeMemberSyntax(
                memberIdentifier,
                openParenthesis,
                dimensions,
                closeParenthesis,
                asKeyword,
                memberType,
                starToken,
                fixedStringLength));
        }

        var endKeyword = MatchToken(SyntaxKind.EndKeyword);
        var endTypeKeyword = MatchToken(SyntaxKind.TypeKeyword);
        ConsumeLineTerminator();
        return new TypeDeclarationSyntax(
            visibilityKeyword,
            typeKeyword,
            identifier,
            members.ToImmutable(),
            endKeyword,
            endTypeKeyword);
    }

    private SyntaxToken MatchTypeMemberName()
    {
        if (Current.Kind == SyntaxKind.IdentifierToken || IsKeyword(Current.Kind))
        {
            return NextToken();
        }

        return MatchToken(SyntaxKind.IdentifierToken);
    }

    private static bool IsKeyword(SyntaxKind kind) =>
        (int)kind >= (int)SyntaxKind.OptionKeyword &&
        (int)kind <= (int)SyntaxKind.IsKeyword;

    private void RecoverToLineEndOrTypeEnd()
    {
        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken &&
               !IsEndPair(SyntaxKind.TypeKeyword))
        {
            NextToken();
        }

        if (Current.Kind == SyntaxKind.NewLineToken)
        {
            NextToken();
        }
    }

    private ConstDeclarationSyntax ParseConstDeclaration(SyntaxToken? visibilityKeyword)
    {
        var constKeyword = MatchToken(SyntaxKind.ConstKeyword);
        var identifier = MatchToken(SyntaxKind.IdentifierToken);

        SyntaxToken? asKeyword = null;
        SyntaxToken? typeToken = null;
        if (Current.Kind == SyntaxKind.AsKeyword)
        {
            asKeyword = NextToken();
            typeToken = MatchTypeToken();
        }

        var equalsToken = MatchToken(SyntaxKind.EqualsToken);
        var value = ParseExpression();
        ConsumeLineTerminator();

        return new ConstDeclarationSyntax(
            visibilityKeyword,
            constKeyword,
            identifier,
            asKeyword,
            typeToken,
            equalsToken,
            value);
    }

    private ModuleVariableDeclarationSyntax ParseModuleVariableDeclaration(SyntaxToken visibilityKeyword)
    {
        var declarators = ParseVariableDeclarators();
        ConsumeLineTerminator();
        return new ModuleVariableDeclarationSyntax(visibilityKeyword, declarators);
    }

    private ImmutableArray<VariableDeclaratorSyntax> ParseVariableDeclarators()
    {
        var declarators = ImmutableArray.CreateBuilder<VariableDeclaratorSyntax>();

        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.ColonToken and not SyntaxKind.EndOfFileToken)
        {
            var identifier = MatchToken(SyntaxKind.IdentifierToken);
            var (openParenthesis, dimensions, closeParenthesis) = ParseArrayDimensions();

            SyntaxToken? asKeyword = null;
            SyntaxToken? typeToken = null;
            if (Current.Kind == SyntaxKind.AsKeyword)
            {
                asKeyword = NextToken();
                typeToken = MatchTypeToken();
            }

            SyntaxToken? commaToken = null;
            if (Current.Kind == SyntaxKind.CommaToken)
            {
                commaToken = NextToken();
            }

            declarators.Add(new VariableDeclaratorSyntax(
                identifier,
                openParenthesis,
                dimensions,
                closeParenthesis,
                asKeyword,
                typeToken,
                commaToken));
            if (commaToken is null)
            {
                break;
            }
        }

        return declarators.ToImmutable();
    }

    private (SyntaxToken? OpenParenthesis, ImmutableArray<ArrayDimensionSyntax> Dimensions, SyntaxToken? CloseParenthesis)
        ParseArrayDimensions()
    {
        if (Current.Kind != SyntaxKind.OpenParenthesisToken)
        {
            return (null, ImmutableArray<ArrayDimensionSyntax>.Empty, null);
        }

        var openParenthesis = NextToken();
        var dimensions = ImmutableArray.CreateBuilder<ArrayDimensionSyntax>();

        if (Current.Kind != SyntaxKind.CloseParenthesisToken)
        {
            while (Current.Kind is not SyntaxKind.CloseParenthesisToken and not SyntaxKind.EndOfFileToken)
            {
                var firstBound = ParseExpression();
                ExpressionSyntax? lowerBound = null;
                SyntaxToken? toKeyword = null;
                var upperBound = firstBound;

                if (Current.Kind == SyntaxKind.ToKeyword)
                {
                    lowerBound = firstBound;
                    toKeyword = NextToken();
                    upperBound = ParseExpression();
                }

                SyntaxToken? commaToken = null;
                if (Current.Kind == SyntaxKind.CommaToken)
                {
                    commaToken = NextToken();
                }

                dimensions.Add(new ArrayDimensionSyntax(lowerBound, toKeyword, upperBound, commaToken));
                if (commaToken is null)
                {
                    break;
                }
            }
        }

        var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);
        return (openParenthesis, dimensions.ToImmutable(), closeParenthesis);
    }

    private static bool IsVisibilityModifier(SyntaxToken token) =>
        token.Kind == SyntaxKind.IdentifierToken &&
        (string.Equals(token.Text, "Public", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(token.Text, "Private", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(token.Text, "Friend", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(token.Text, "Global", StringComparison.OrdinalIgnoreCase));

    private bool LooksLikeModuleVariableDeclaration()
    {
        if ((!IsVisibilityModifier(Current) && Current.Kind != SyntaxKind.DimKeyword) ||
            Peek(1).Kind != SyntaxKind.IdentifierToken)
        {
            return false;
        }

        return Peek(2).Kind is SyntaxKind.AsKeyword or SyntaxKind.CommaToken or SyntaxKind.OpenParenthesisToken or
            SyntaxKind.NewLineToken or SyntaxKind.ColonToken or SyntaxKind.EndOfFileToken;
    }

    private bool IsOptionDirective(string name) =>
        Current.Kind == SyntaxKind.OptionKeyword &&
        Peek(1).Kind == SyntaxKind.IdentifierToken &&
        string.Equals(Peek(1).Text, name, StringComparison.OrdinalIgnoreCase);

    private bool IsAttributeLine() =>
        Current.Kind == SyntaxKind.IdentifierToken &&
        string.Equals(Current.Text, "Attribute", StringComparison.OrdinalIgnoreCase) &&
        Peek(1).Kind == SyntaxKind.IdentifierToken;

    private AttributeSyntax ParseAttribute()
    {
        var attributeKeyword = NextToken();
        var tokens = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            tokens.Add(NextToken());
        }

        ConsumeLineTerminator();
        return new AttributeSyntax(attributeKeyword, tokens.ToImmutable());
    }

    private OptionExplicitSyntax ParseOptionExplicit()
    {
        var optionKeyword = MatchToken(SyntaxKind.OptionKeyword);
        var explicitKeyword = MatchToken(SyntaxKind.ExplicitKeyword);
        ConsumeLineTerminator();
        return new OptionExplicitSyntax(optionKeyword, explicitKeyword);
    }

    private OptionBaseSyntax ParseOptionBase()
    {
        var optionKeyword = MatchToken(SyntaxKind.OptionKeyword);
        var baseIdentifier = MatchToken(SyntaxKind.IdentifierToken);
        var valueToken = MatchToken(SyntaxKind.IntegerLiteralToken);
        if (valueToken.Text is not "0" and not "1")
        {
            ReportUnexpected(valueToken, "0 or 1");
        }

        ConsumeLineTerminator();
        return new OptionBaseSyntax(optionKeyword, baseIdentifier, valueToken);
    }

    private OptionCompareSyntax ParseOptionCompare()
    {
        var optionKeyword = MatchToken(SyntaxKind.OptionKeyword);
        var compareIdentifier = MatchToken(SyntaxKind.IdentifierToken);
        var modeToken = MatchToken(SyntaxKind.IdentifierToken);
        if (!string.Equals(modeToken.Text, "Text", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(modeToken.Text, "Binary", StringComparison.OrdinalIgnoreCase))
        {
            ReportUnexpected(modeToken, "Text or Binary");
        }

        ConsumeLineTerminator();
        return new OptionCompareSyntax(optionKeyword, compareIdentifier, modeToken);
    }

    private SubDeclarationSyntax ParseSubDeclaration(SyntaxToken? visibilityKeyword = null)
    {
        var subKeyword = MatchToken(SyntaxKind.SubKeyword);
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var openParenthesis = MatchToken(SyntaxKind.OpenParenthesisToken);
        var parameters = ParseParameters();
        var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);
        ConsumeLineTerminator();
        var statements = ParseProcedureStatements(SyntaxKind.SubKeyword);
        var endKeyword = MatchToken(SyntaxKind.EndKeyword);
        var endSubKeyword = MatchToken(SyntaxKind.SubKeyword);
        ConsumeLineTerminator();

        return new SubDeclarationSyntax(
            subKeyword,
            identifier,
            openParenthesis,
            parameters,
            closeParenthesis,
            statements,
            endKeyword,
            endSubKeyword,
            visibilityKeyword);
    }

    private FunctionDeclarationSyntax ParseFunctionDeclaration(SyntaxToken? visibilityKeyword = null)
    {
        var functionKeyword = MatchToken(SyntaxKind.FunctionKeyword);
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var openParenthesis = MatchToken(SyntaxKind.OpenParenthesisToken);
        var parameters = ParseParameters();
        var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);

        // Omitting As is legal and makes the function return Variant, exactly as Declare Function
        // already allows. Requiring it here derailed every untyped Function in real code.
        SyntaxToken? asKeyword = null;
        SyntaxToken? returnType = null;
        if (Current.Kind == SyntaxKind.AsKeyword)
        {
            asKeyword = NextToken();
            returnType = MatchTypeToken();
        }

        ConsumeLineTerminator();
        var statements = ParseProcedureStatements(SyntaxKind.FunctionKeyword);
        var endKeyword = MatchToken(SyntaxKind.EndKeyword);
        var endFunctionKeyword = MatchToken(SyntaxKind.FunctionKeyword);
        ConsumeLineTerminator();

        return new FunctionDeclarationSyntax(
            functionKeyword,
            identifier,
            openParenthesis,
            parameters,
            closeParenthesis,
            asKeyword,
            returnType,
            statements,
            endKeyword,
            endFunctionKeyword,
            visibilityKeyword);
    }

    private ImmutableArray<StatementSyntax> ParseProcedureStatements(SyntaxKind endingKeyword) =>
        ParseStatementsUntil(() => IsEndPair(endingKeyword));

    private ImmutableArray<StatementSyntax> ParseStatementsUntil(Func<bool> isTerminator)
    {
        var statements = ImmutableArray.CreateBuilder<StatementSyntax>();
        while (Current.Kind != SyntaxKind.EndOfFileToken && !isTerminator())
        {
            if (Current.Kind == SyntaxKind.NewLineToken)
            {
                NextToken();
                continue;
            }

            var start = _position;
            statements.Add(ParseStatement());
            if (_position == start)
            {
                NextToken();
            }

            ConsumeLineTerminator();
        }

        return statements.ToImmutable();
    }

    private ImmutableArray<StatementSyntax> ParseInlineStatementsUntil(Func<bool> isTerminator)
    {
        var statements = ImmutableArray.CreateBuilder<StatementSyntax>();
        while (!IsPhysicalLineTerminator(Current.Kind) && !isTerminator())
        {
            var start = _position;
            statements.Add(ParseStatement());
            if (_position == start)
            {
                NextToken();
            }

            if (Current.Kind == SyntaxKind.ColonToken)
            {
                NextToken();
                continue;
            }

            break;
        }

        return statements.ToImmutable();
    }

    private ImmutableArray<ParameterSyntax> ParseParameters()
    {
        var parameters = ImmutableArray.CreateBuilder<ParameterSyntax>();
        if (Current.Kind == SyntaxKind.CloseParenthesisToken)
        {
            return parameters.ToImmutable();
        }

        while (Current.Kind is not SyntaxKind.CloseParenthesisToken and not SyntaxKind.EndOfFileToken)
        {
            parameters.Add(ParseParameter());
            if (Current.Kind != SyntaxKind.CommaToken)
            {
                break;
            }

            NextToken();
        }

        return parameters.ToImmutable();
    }

    private ParameterSyntax ParseParameter()
    {
        SyntaxToken? optionalKeyword = null;
        if (Current.Kind == SyntaxKind.OptionalKeyword)
        {
            optionalKeyword = NextToken();
        }

        SyntaxToken? passingModeKeyword = null;
        if (Current.Kind is SyntaxKind.ByRefKeyword or SyntaxKind.ByValKeyword)
        {
            passingModeKeyword = NextToken();
        }

        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var (openParenthesis, dimensions, closeParenthesis) = ParseArrayDimensions();
        var asKeyword = MatchToken(SyntaxKind.AsKeyword);
        var typeToken = MatchTypeToken();

        SyntaxToken? equalsToken = null;
        ExpressionSyntax? defaultValue = null;
        if (Current.Kind == SyntaxKind.EqualsToken)
        {
            equalsToken = NextToken();
            defaultValue = ParseExpression();
        }

        return new ParameterSyntax(
            passingModeKeyword,
            identifier,
            asKeyword,
            typeToken,
            optionalKeyword,
            equalsToken,
            defaultValue,
            openParenthesis,
            dimensions,
            closeParenthesis);
    }

    private StatementSyntax ParseStatement()
    {
        return Current.Kind switch
        {
            SyntaxKind.DimKeyword => ParseDimStatement(),
            SyntaxKind.ReDimKeyword => ParseReDimStatement(),
            SyntaxKind.EraseKeyword => ParseEraseStatement(),
            SyntaxKind.StaticKeyword => ParseStaticStatement(),
            SyntaxKind.IfKeyword => ParseIfStatement(),
            SyntaxKind.ForKeyword when Peek(1).Kind == SyntaxKind.EachKeyword => ParseForEachStatement(),
            SyntaxKind.ForKeyword => ParseForStatement(),
            SyntaxKind.WhileKeyword => ParseWhileStatement(),
            SyntaxKind.DoKeyword => ParseDoStatement(),
            SyntaxKind.WithKeyword => ParseWithStatement(),
            SyntaxKind.ExitKeyword => ParseExitStatement(),
            SyntaxKind.SelectKeyword => ParseSelectCaseStatement(),
            SyntaxKind.DebugKeyword => ParseDebugPrintStatement(),
            SyntaxKind.CallKeyword => ParseInvocationStatement(),
            SyntaxKind.IdentifierToken when IsFileStatementKeyword("Open") => ParseOpenStatement(),
            SyntaxKind.IdentifierToken when IsFileStatementKeyword("Close") => ParseCloseStatement(),
            SyntaxKind.IdentifierToken when IsFileStatementKeyword("Get") => ParseGetOrPutStatement(isGet: true),
            SyntaxKind.IdentifierToken when IsFileStatementKeyword("Put") => ParseGetOrPutStatement(isGet: false),
            SyntaxKind.IdentifierToken when IsFileStatementKeyword("Seek") => ParseSeekStatement(),
            SyntaxKind.IdentifierToken when LooksLikeArrayElementAssignment() => ParseArrayElementAssignmentStatement(),
            SyntaxKind.IdentifierToken when LooksLikeMemberAssignment() => ParseMemberAssignmentStatement(),
            SyntaxKind.DotToken when LooksLikeMemberAssignment() => ParseMemberAssignmentStatement(),
            SyntaxKind.IdentifierToken when Peek(1).Kind == SyntaxKind.EqualsToken => ParseAssignmentStatement(),
            SyntaxKind.IdentifierToken => ParseInvocationStatement(),
            _ => ParseSkippedStatement()
        };
    }

    private bool LooksLikeArrayElementAssignment()
    {
        if (Current.Kind != SyntaxKind.IdentifierToken || Peek(1).Kind != SyntaxKind.OpenParenthesisToken)
        {
            return false;
        }

        var depth = 0;
        for (var offset = 1; ; offset++)
        {
            var kind = Peek(offset).Kind;
            switch (kind)
            {
                case SyntaxKind.OpenParenthesisToken:
                    depth++;
                    break;
                case SyntaxKind.CloseParenthesisToken:
                    depth--;
                    if (depth == 0)
                    {
                        return Peek(offset + 1).Kind == SyntaxKind.EqualsToken;
                    }
                    break;
                case SyntaxKind.NewLineToken:
                case SyntaxKind.ColonToken:
                case SyntaxKind.EndOfFileToken:
                    return false;
            }
        }
    }

    private bool LooksLikeMemberAssignment()
    {
        if (Current.Kind is not SyntaxKind.IdentifierToken and not SyntaxKind.DotToken)
        {
            return false;
        }

        var offset = Current.Kind == SyntaxKind.IdentifierToken ? 1 : 0;
        var sawMember = false;

        while (true)
        {
            var kind = Peek(offset).Kind;
            if (kind == SyntaxKind.DotToken)
            {
                sawMember = true;
                offset++;
                var memberKind = Peek(offset).Kind;
                if (memberKind != SyntaxKind.IdentifierToken && !IsKeyword(memberKind))
                {
                    return false;
                }

                offset++;
                continue;
            }

            if (kind == SyntaxKind.OpenParenthesisToken)
            {
                var depth = 0;
                for (;; offset++)
                {
                    kind = Peek(offset).Kind;
                    if (kind == SyntaxKind.OpenParenthesisToken)
                    {
                        depth++;
                    }
                    else if (kind == SyntaxKind.CloseParenthesisToken)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            offset++;
                            break;
                        }
                    }
                    else if (kind is SyntaxKind.NewLineToken or SyntaxKind.ColonToken or SyntaxKind.EndOfFileToken)
                    {
                        return false;
                    }
                }

                continue;
            }

            break;
        }

        return sawMember && Peek(offset).Kind == SyntaxKind.EqualsToken;
    }

    private DimStatementSyntax ParseDimStatement()
    {
        var dimKeyword = MatchToken(SyntaxKind.DimKeyword);
        return new DimStatementSyntax(dimKeyword, ParseVariableDeclarators());
    }

    private ReDimStatementSyntax ParseReDimStatement()
    {
        var reDimKeyword = MatchToken(SyntaxKind.ReDimKeyword);
        SyntaxToken? preserveKeyword = null;
        if (Current.Kind == SyntaxKind.PreserveKeyword)
        {
            preserveKeyword = NextToken();
        }

        var declarators = ParseVariableDeclarators();

        // ReDim Section(0).Bytes(0) redimensions an array that lives inside a UDT element. The
        // bound model still expects a plain variable, so the construct is reported and the rest of
        // the line is discarded. Without this the dot derailed the statement loop and took the
        // remainder of the procedure with it - it was the first error in four corpus modules.
        if (Current.Kind == SyntaxKind.DotToken)
        {
            _diagnostics.Add(new Diagnostic(
                "VB6P0002",
                DiagnosticSeverity.Error,
                "ReDim of a qualified target such as 'Item(0).Field(0)' is not implemented yet.",
                Current.Span,
                _text.FilePath));

            while (Current.Kind is not SyntaxKind.NewLineToken
                   and not SyntaxKind.ColonToken
                   and not SyntaxKind.EndOfFileToken)
            {
                NextToken();
            }
        }

        return new ReDimStatementSyntax(reDimKeyword, preserveKeyword, declarators);
    }

    /// <summary>
    /// File I/O statement words are recognized at statement position only. Reserving Open, Close,
    /// Get, Put and Seek globally would repeat the mistake Option Base already taught: these are
    /// ordinary identifiers everywhere else.
    ///
    /// A following '=' means an assignment to a variable of that name, which wins.
    /// </summary>
    private bool IsFileStatementKeyword(string keyword) =>
        string.Equals(Current.Text, keyword, StringComparison.OrdinalIgnoreCase) &&
        Peek(1).Kind is not SyntaxKind.EqualsToken and not SyntaxKind.DotToken;

    private FileNumberSyntax ParseFileNumber()
    {
        SyntaxToken? hashToken = null;
        if (Current.Kind == SyntaxKind.HashToken)
        {
            hashToken = NextToken();
        }

        return new FileNumberSyntax(hashToken, ParseExpression());
    }

    private OpenStatementSyntax ParseOpenStatement()
    {
        var openKeyword = NextToken();
        var path = ParseExpression();
        var forKeyword = MatchToken(SyntaxKind.ForKeyword);
        var mode = NextToken();
        var asKeyword = MatchToken(SyntaxKind.AsKeyword);
        var fileNumber = ParseFileNumber();

        SyntaxToken? lenKeyword = null;
        SyntaxToken? lenEquals = null;
        ExpressionSyntax? recordLength = null;
        if (string.Equals(Current.Text, "Len", StringComparison.OrdinalIgnoreCase) &&
            Peek(1).Kind == SyntaxKind.EqualsToken)
        {
            lenKeyword = NextToken();
            lenEquals = NextToken();
            recordLength = ParseExpression();
        }

        return new OpenStatementSyntax(
            openKeyword,
            path,
            forKeyword,
            mode,
            asKeyword,
            fileNumber,
            lenKeyword,
            lenEquals,
            recordLength);
    }

    private CloseStatementSyntax ParseCloseStatement()
    {
        var closeKeyword = NextToken();
        var fileNumbers = ImmutableArray.CreateBuilder<FileNumberSyntax>();

        // Close without a file number closes every open file in VB6.
        while (Current.Kind is not SyntaxKind.NewLineToken
               and not SyntaxKind.ColonToken
               and not SyntaxKind.EndOfFileToken)
        {
            fileNumbers.Add(ParseFileNumber());
            if (Current.Kind != SyntaxKind.CommaToken)
            {
                break;
            }

            NextToken();
        }

        return new CloseStatementSyntax(closeKeyword, fileNumbers.ToImmutable());
    }

    private StatementSyntax ParseGetOrPutStatement(bool isGet)
    {
        var keyword = NextToken();
        var fileNumber = ParseFileNumber();
        MatchToken(SyntaxKind.CommaToken);

        // Get #1, , target keeps the current file position.
        ExpressionSyntax? recordPosition = null;
        if (Current.Kind != SyntaxKind.CommaToken)
        {
            recordPosition = ParseExpression();
        }

        MatchToken(SyntaxKind.CommaToken);
        var target = ParseExpression();

        return isGet
            ? new GetStatementSyntax(keyword, fileNumber, recordPosition, target)
            : new PutStatementSyntax(keyword, fileNumber, recordPosition, target);
    }

    private SeekStatementSyntax ParseSeekStatement()
    {
        var seekKeyword = NextToken();
        var fileNumber = ParseFileNumber();
        MatchToken(SyntaxKind.CommaToken);
        return new SeekStatementSyntax(seekKeyword, fileNumber, ParseExpression());
    }

    private EraseStatementSyntax ParseEraseStatement()
    {
        var eraseKeyword = MatchToken(SyntaxKind.EraseKeyword);
        var identifiers = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.ColonToken and not SyntaxKind.EndOfFileToken)
        {
            identifiers.Add(MatchToken(SyntaxKind.IdentifierToken));
            if (Current.Kind != SyntaxKind.CommaToken)
            {
                break;
            }

            NextToken();
        }

        return new EraseStatementSyntax(eraseKeyword, identifiers.ToImmutable());
    }

    private StaticStatementSyntax ParseStaticStatement()
    {
        var staticKeyword = MatchToken(SyntaxKind.StaticKeyword);
        return new StaticStatementSyntax(staticKeyword, ParseVariableDeclarators());
    }

    private AssignmentStatementSyntax ParseAssignmentStatement()
    {
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var equalsToken = MatchToken(SyntaxKind.EqualsToken);
        var expression = ParseExpression();
        return new AssignmentStatementSyntax(identifier, equalsToken, expression);
    }

    private ArrayElementAssignmentStatementSyntax ParseArrayElementAssignmentStatement()
    {
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var openParenthesis = MatchToken(SyntaxKind.OpenParenthesisToken);
        var indices = ParseArguments(SyntaxKind.CloseParenthesisToken);
        var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);
        var equalsToken = MatchToken(SyntaxKind.EqualsToken);
        var expression = ParseExpression();
        return new ArrayElementAssignmentStatementSyntax(
            identifier,
            openParenthesis,
            indices,
            closeParenthesis,
            equalsToken,
            expression);
    }

    private MemberAssignmentStatementSyntax ParseMemberAssignmentStatement()
    {
        var target = ParsePrimaryExpression();
        var equalsToken = MatchToken(SyntaxKind.EqualsToken);
        var expression = ParseExpression();
        return new MemberAssignmentStatementSyntax(target, equalsToken, expression);
    }

    private IfStatementSyntax ParseIfStatement()
    {
        var ifKeyword = MatchToken(SyntaxKind.IfKeyword);
        var condition = ParseExpression();
        var thenKeyword = MatchToken(SyntaxKind.ThenKeyword);

        if (Current.Kind != SyntaxKind.NewLineToken)
        {
            var statements = ParseInlineStatementsUntil(() => Current.Kind == SyntaxKind.ElseKeyword);
            SyntaxToken? elseKeyword = null;
            var elseStatements = ImmutableArray<StatementSyntax>.Empty;

            if (Current.Kind == SyntaxKind.ElseKeyword)
            {
                elseKeyword = NextToken();
                elseStatements = ParseInlineStatementsUntil(() => false);
            }

            return new IfStatementSyntax(
                ifKeyword,
                condition,
                thenKeyword,
                statements,
                ImmutableArray<ElseIfClauseSyntax>.Empty,
                elseKeyword,
                elseStatements,
                null,
                null,
                true);
        }

        NextToken();
        var thenStatements = ParseStatementsUntil(IsIfBranchTerminator);
        var elseIfClauses = ImmutableArray.CreateBuilder<ElseIfClauseSyntax>();

        while (Current.Kind == SyntaxKind.ElseIfKeyword)
        {
            var elseIfKeyword = NextToken();
            var elseIfCondition = ParseExpression();
            var elseIfThenKeyword = MatchToken(SyntaxKind.ThenKeyword);
            ConsumeLineTerminator();
            var elseIfStatements = ParseStatementsUntil(IsIfBranchTerminator);
            elseIfClauses.Add(new ElseIfClauseSyntax(
                elseIfKeyword,
                elseIfCondition,
                elseIfThenKeyword,
                elseIfStatements));
        }

        SyntaxToken? multilineElseKeyword = null;
        var multilineElseStatements = ImmutableArray<StatementSyntax>.Empty;
        if (Current.Kind == SyntaxKind.ElseKeyword)
        {
            multilineElseKeyword = NextToken();
            ConsumeLineTerminator();
            multilineElseStatements = ParseStatementsUntil(() => IsEndPair(SyntaxKind.IfKeyword));
        }

        var endKeyword = MatchToken(SyntaxKind.EndKeyword);
        var ifEndKeyword = MatchToken(SyntaxKind.IfKeyword);
        return new IfStatementSyntax(
            ifKeyword,
            condition,
            thenKeyword,
            thenStatements,
            elseIfClauses.ToImmutable(),
            multilineElseKeyword,
            multilineElseStatements,
            endKeyword,
            ifEndKeyword,
            false);
    }

    private bool IsIfBranchTerminator() =>
        Current.Kind is SyntaxKind.ElseIfKeyword or SyntaxKind.ElseKeyword || IsEndPair(SyntaxKind.IfKeyword);

    private ForEachStatementSyntax ParseForEachStatement()
    {
        var forKeyword = MatchToken(SyntaxKind.ForKeyword);
        var eachKeyword = MatchToken(SyntaxKind.EachKeyword);
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var inKeyword = MatchToken(SyntaxKind.InKeyword);
        var collection = ParseExpression();

        ConsumeLineTerminator();
        var statements = ParseStatementsUntil(() => Current.Kind == SyntaxKind.NextKeyword);
        var nextKeyword = MatchToken(SyntaxKind.NextKeyword);
        SyntaxToken? nextIdentifier = null;
        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            nextIdentifier = NextToken();
        }

        return new ForEachStatementSyntax(
            forKeyword,
            eachKeyword,
            identifier,
            inKeyword,
            collection,
            statements,
            nextKeyword,
            nextIdentifier);
    }

    private ForStatementSyntax ParseForStatement()
    {
        var forKeyword = MatchToken(SyntaxKind.ForKeyword);
        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        var equalsToken = MatchToken(SyntaxKind.EqualsToken);
        var initialValue = ParseExpression();
        var toKeyword = MatchToken(SyntaxKind.ToKeyword);
        var limit = ParseExpression();

        SyntaxToken? stepKeyword = null;
        ExpressionSyntax? step = null;
        if (Current.Kind == SyntaxKind.StepKeyword)
        {
            stepKeyword = NextToken();
            step = ParseExpression();
        }

        ConsumeLineTerminator();
        var statements = ParseStatementsUntil(() => Current.Kind == SyntaxKind.NextKeyword);
        var nextKeyword = MatchToken(SyntaxKind.NextKeyword);
        SyntaxToken? nextIdentifier = null;
        if (Current.Kind == SyntaxKind.IdentifierToken)
        {
            nextIdentifier = NextToken();
        }

        return new ForStatementSyntax(
            forKeyword,
            identifier,
            equalsToken,
            initialValue,
            toKeyword,
            limit,
            stepKeyword,
            step,
            statements,
            nextKeyword,
            nextIdentifier);
    }

    private WhileStatementSyntax ParseWhileStatement()
    {
        var whileKeyword = MatchToken(SyntaxKind.WhileKeyword);
        var condition = ParseExpression();
        ConsumeLineTerminator();
        var statements = ParseStatementsUntil(() => Current.Kind == SyntaxKind.WendKeyword);
        var wendKeyword = MatchToken(SyntaxKind.WendKeyword);
        return new WhileStatementSyntax(whileKeyword, condition, statements, wendKeyword);
    }

    private DoStatementSyntax ParseDoStatement()
    {
        var doKeyword = MatchToken(SyntaxKind.DoKeyword);
        SyntaxToken? preConditionKeyword = null;
        ExpressionSyntax? preCondition = null;

        if (Current.Kind is SyntaxKind.WhileKeyword or SyntaxKind.UntilKeyword)
        {
            preConditionKeyword = NextToken();
            preCondition = ParseExpression();
        }

        ConsumeLineTerminator();
        var statements = ParseStatementsUntil(() => Current.Kind == SyntaxKind.LoopKeyword);
        var loopKeyword = MatchToken(SyntaxKind.LoopKeyword);

        SyntaxToken? postConditionKeyword = null;
        ExpressionSyntax? postCondition = null;
        if (Current.Kind is SyntaxKind.WhileKeyword or SyntaxKind.UntilKeyword)
        {
            postConditionKeyword = NextToken();
            postCondition = ParseExpression();
        }

        return new DoStatementSyntax(
            doKeyword,
            preConditionKeyword,
            preCondition,
            statements,
            loopKeyword,
            postConditionKeyword,
            postCondition);
    }

    private WithStatementSyntax ParseWithStatement()
    {
        var withKeyword = MatchToken(SyntaxKind.WithKeyword);
        var expression = ParseExpression();
        ConsumeLineTerminator();
        var statements = ParseStatementsUntil(() => IsEndPair(SyntaxKind.WithKeyword));
        var endKeyword = MatchToken(SyntaxKind.EndKeyword);
        var endWithKeyword = MatchToken(SyntaxKind.WithKeyword);
        return new WithStatementSyntax(
            withKeyword,
            expression,
            statements,
            endKeyword,
            endWithKeyword);
    }

    private ExitStatementSyntax ParseExitStatement()
    {
        var exitKeyword = MatchToken(SyntaxKind.ExitKeyword);
        SyntaxToken targetKeyword;

        if (Current.Kind is SyntaxKind.ForKeyword or SyntaxKind.DoKeyword
            or SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword)
        {
            targetKeyword = NextToken();
        }
        else
        {
            targetKeyword = MatchToken(SyntaxKind.ForKeyword);
        }

        return new ExitStatementSyntax(exitKeyword, targetKeyword);
    }

    private SelectCaseStatementSyntax ParseSelectCaseStatement()
    {
        var selectKeyword = MatchToken(SyntaxKind.SelectKeyword);
        var caseKeyword = MatchToken(SyntaxKind.CaseKeyword);
        var expression = ParseExpression();
        ConsumeLineTerminator();

        var cases = ImmutableArray.CreateBuilder<CaseBlockSyntax>();
        while (Current.Kind == SyntaxKind.CaseKeyword)
        {
            cases.Add(ParseCaseBlock());
        }

        var endKeyword = MatchToken(SyntaxKind.EndKeyword);
        var endSelectKeyword = MatchToken(SyntaxKind.SelectKeyword);
        return new SelectCaseStatementSyntax(
            selectKeyword,
            caseKeyword,
            expression,
            cases.ToImmutable(),
            endKeyword,
            endSelectKeyword);
    }

    private CaseBlockSyntax ParseCaseBlock()
    {
        var caseKeyword = MatchToken(SyntaxKind.CaseKeyword);
        var clauses = ImmutableArray.CreateBuilder<CaseClauseSyntax>();

        if (Current.Kind == SyntaxKind.ElseKeyword)
        {
            clauses.Add(new CaseElseClauseSyntax(NextToken()));
        }
        else
        {
            while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
            {
                clauses.Add(ParseCaseClause());
                if (Current.Kind != SyntaxKind.CommaToken)
                {
                    break;
                }

                NextToken();
            }
        }

        ConsumeLineTerminator();
        var statements = ParseStatementsUntil(() =>
            Current.Kind == SyntaxKind.CaseKeyword || IsEndPair(SyntaxKind.SelectKeyword));
        return new CaseBlockSyntax(caseKeyword, clauses.ToImmutable(), statements);
    }

    private CaseClauseSyntax ParseCaseClause()
    {
        if (Current.Kind == SyntaxKind.IsKeyword)
        {
            var isKeyword = NextToken();
            var operatorToken = MatchComparisonOperator();
            var value = ParseExpression();
            return new CaseRelationalClauseSyntax(isKeyword, operatorToken, value);
        }

        var lowerBound = ParseExpression();
        if (Current.Kind == SyntaxKind.ToKeyword)
        {
            var toKeyword = NextToken();
            var upperBound = ParseExpression();
            return new CaseRangeClauseSyntax(lowerBound, toKeyword, upperBound);
        }

        return new CaseValueClauseSyntax(lowerBound);
    }

    private SyntaxToken MatchComparisonOperator()
    {
        if (Current.Kind is SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken or
            SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or
            SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken)
        {
            return NextToken();
        }

        return MatchToken(SyntaxKind.EqualsToken);
    }

    private InvocationStatementSyntax ParseInvocationStatement()
    {
        SyntaxToken? callKeyword = null;
        if (Current.Kind == SyntaxKind.CallKeyword)
        {
            callKeyword = NextToken();
        }

        var identifier = MatchToken(SyntaxKind.IdentifierToken);
        SyntaxToken? openParenthesis = null;
        SyntaxToken? closeParenthesis = null;
        ImmutableArray<ExpressionSyntax> arguments;

        // Only a Call statement has a parenthesized argument list. Without Call, a leading
        // parenthesis belongs to the first argument, which is what makes Foo (x) pass x by value
        // in VB6 while Call Foo(x) passes it by reference.
        if (callKeyword is not null && Current.Kind == SyntaxKind.OpenParenthesisToken)
        {
            openParenthesis = NextToken();
            arguments = ParseArguments(SyntaxKind.CloseParenthesisToken);
            closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);
        }
        else
        {
            arguments = ParseArguments(null);
        }

        return new InvocationStatementSyntax(
            callKeyword,
            identifier,
            openParenthesis,
            arguments,
            closeParenthesis);
    }

    private ImmutableArray<ExpressionSyntax> ParseArguments(SyntaxKind? terminator)
    {
        var arguments = ImmutableArray.CreateBuilder<ExpressionSyntax>();
        if ((terminator is not null && Current.Kind == terminator) ||
            (terminator is null && IsLineTerminator(Current.Kind)))
        {
            return arguments.ToImmutable();
        }

        while (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            if (terminator is not null && Current.Kind == terminator)
            {
                break;
            }

            if (terminator is null && IsLineTerminator(Current.Kind))
            {
                break;
            }

            arguments.Add(ParseExpression());
            if (Current.Kind != SyntaxKind.CommaToken)
            {
                break;
            }

            NextToken();
        }

        return arguments.ToImmutable();
    }

    private DebugPrintStatementSyntax ParseDebugPrintStatement()
    {
        var debugKeyword = MatchToken(SyntaxKind.DebugKeyword);
        var dotToken = MatchToken(SyntaxKind.DotToken);
        var printKeyword = MatchToken(SyntaxKind.PrintKeyword);
        var expression = ParseExpression();
        return new DebugPrintStatementSyntax(debugKeyword, dotToken, printKeyword, expression);
    }

    private SkippedStatementSyntax ParseSkippedStatement()
    {
        var token = NextToken();
        ReportUnexpected(token, "statement");

        while (Current.Kind is not SyntaxKind.NewLineToken and not SyntaxKind.EndOfFileToken)
        {
            NextToken();
        }

        return new SkippedStatementSyntax(token);
    }

    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        ExpressionSyntax left;
        var unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            var operatorToken = NextToken();
            var operand = ParseExpression(unaryPrecedence);
            left = new UnaryExpressionSyntax(operatorToken, operand);
        }
        else
        {
            left = ParsePrimaryExpression();
        }

        while (true)
        {
            var precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence)
            {
                break;
            }

            var operatorToken = NextToken();
            var right = operatorToken.Kind == SyntaxKind.CaretToken
                ? ParseExponentOperand()
                : ParseExpression(precedence);
            left = new BinaryExpressionSyntax(left, operatorToken, right);
        }

        return left;
    }

    private ExpressionSyntax ParseExponentOperand()
    {
        if (Current.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken)
        {
            var operatorToken = NextToken();
            return new UnaryExpressionSyntax(operatorToken, ParseExponentOperand());
        }

        return ParsePrimaryExpression();
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        ExpressionSyntax expression;
        if (Current.Kind == SyntaxKind.TypeOfKeyword)
        {
            // TypeOf x Is T is one complete boolean expression, so the Is and the type name are
            // consumed here rather than left to the binary operator loop.
            var typeOfKeyword = NextToken();
            var operand = ParsePrimaryExpression();
            var isKeyword = MatchToken(SyntaxKind.IsKeyword);
            var typeToken = MatchToken(SyntaxKind.IdentifierToken);
            return new TypeOfExpressionSyntax(typeOfKeyword, operand, isKeyword, typeToken);
        }

        if (Current.Kind == SyntaxKind.DotToken)
        {
            expression = new WithReceiverExpressionSyntax();
        }
        else if (Current.Kind == SyntaxKind.OpenParenthesisToken)
        {
            var openParenthesis = NextToken();
            var inner = ParseExpression();
            var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);
            expression = new ParenthesizedExpressionSyntax(openParenthesis, inner, closeParenthesis);
        }
        else if (Current.Kind is SyntaxKind.IntegerLiteralToken or SyntaxKind.FloatingLiteralToken or SyntaxKind.StringLiteralToken or
                 SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword)
        {
            expression = new LiteralExpressionSyntax(NextToken());
        }
        else if (Current.Kind == SyntaxKind.IdentifierToken && Peek(1).Kind == SyntaxKind.OpenParenthesisToken)
        {
            var identifier = NextToken();
            var openParenthesis = NextToken();
            var arguments = ParseArguments(SyntaxKind.CloseParenthesisToken);
            var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);
            expression = new InvocationExpressionSyntax(identifier, openParenthesis, arguments, closeParenthesis);
        }
        else
        {
            expression = new NameExpressionSyntax(MatchToken(SyntaxKind.IdentifierToken));
        }

        while (true)
        {
            if (Current.Kind == SyntaxKind.DotToken)
            {
                var dotToken = NextToken();
                var memberToken = MatchTypeMemberName();
                expression = new MemberAccessExpressionSyntax(expression, dotToken, memberToken);
                continue;
            }

            if (Current.Kind == SyntaxKind.OpenParenthesisToken)
            {
                var openParenthesis = NextToken();
                var indices = ParseArguments(SyntaxKind.CloseParenthesisToken);
                var closeParenthesis = MatchToken(SyntaxKind.CloseParenthesisToken);
                expression = new ElementAccessExpressionSyntax(
                    expression,
                    openParenthesis,
                    indices,
                    closeParenthesis);
                continue;
            }

            break;
        }

        return expression;
    }

    private SyntaxToken MatchTypeToken()
    {
        if (Current.Kind is SyntaxKind.ByteKeyword or SyntaxKind.IntegerKeyword or SyntaxKind.LongKeyword or
            SyntaxKind.SingleKeyword or SyntaxKind.DoubleKeyword or SyntaxKind.IdentifierToken)
        {
            return NextToken();
        }

        return MatchToken(SyntaxKind.IdentifierToken);
    }

    private void ConsumeLineTerminator()
    {
        if (Current.Kind == SyntaxKind.ColonToken)
        {
            NextToken();
            return;
        }

        if (Current.Kind == SyntaxKind.NewLineToken)
        {
            NextToken();
            return;
        }

        if (Current.Kind != SyntaxKind.EndOfFileToken)
        {
            ReportUnexpected(Current, "end of line");
        }
    }

    private void SkipNewLines()
    {
        while (Current.Kind == SyntaxKind.NewLineToken)
        {
            NextToken();
        }
    }

    private bool IsEndPair(SyntaxKind secondKind) =>
        Current.Kind == SyntaxKind.EndKeyword && Peek(1).Kind == secondKind;

    private static bool IsLineTerminator(SyntaxKind kind) =>
        kind is SyntaxKind.NewLineToken or SyntaxKind.ColonToken or SyntaxKind.EndOfFileToken or SyntaxKind.ElseKeyword;

    private static bool IsPhysicalLineTerminator(SyntaxKind kind) =>
        kind is SyntaxKind.NewLineToken or SyntaxKind.EndOfFileToken;

    private SyntaxToken MatchToken(SyntaxKind kind)
    {
        if (Current.Kind == kind)
        {
            return NextToken();
        }

        ReportUnexpected(Current, kind.ToString());
        return new SyntaxToken(
            kind,
            new TextSpan(Current.Span.Start, 0),
            string.Empty,
            null,
            ImmutableArray<SyntaxTrivia>.Empty);
    }

    private SyntaxToken NextToken()
    {
        var current = Current;
        if (_position < _tokens.Length - 1)
        {
            _position++;
        }

        return current;
    }

    private SyntaxToken Current => Peek(0);

    private SyntaxToken Peek(int offset)
    {
        var index = _position + offset;
        if (index >= _tokens.Length)
        {
            return _tokens[^1];
        }

        return _tokens[index];
    }

    private void ReportUnexpected(SyntaxToken token, string expected)
    {
        _diagnostics.Add(new Diagnostic(
            "VB6P0001",
            DiagnosticSeverity.Error,
            $"Unexpected token '{token.Kind}', expected {expected}.",
            token.Span,
            _text.FilePath));
    }

    private static int GetUnaryPrecedence(SyntaxKind kind) => kind switch
    {
        SyntaxKind.PlusToken or SyntaxKind.MinusToken => 13,
        SyntaxKind.NotKeyword => 6,
        _ => 0
    };

    private static int GetBinaryPrecedence(SyntaxKind kind) => kind switch
    {
        SyntaxKind.CaretToken => 14,
        SyntaxKind.StarToken or SyntaxKind.SlashToken => 12,
        SyntaxKind.BackslashToken => 11,
        SyntaxKind.ModKeyword => 10,
        SyntaxKind.PlusToken or SyntaxKind.MinusToken => 9,
        SyntaxKind.AmpersandToken => 8,
        SyntaxKind.EqualsToken or SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or
        SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken or SyntaxKind.LessGreaterToken or
        SyntaxKind.LikeKeyword or SyntaxKind.IsKeyword => 7,
        SyntaxKind.AndKeyword => 5,
        SyntaxKind.OrKeyword => 4,
        SyntaxKind.XorKeyword => 3,
        SyntaxKind.EqvKeyword => 2,
        SyntaxKind.ImpKeyword => 1,
        _ => 0
    };
}
