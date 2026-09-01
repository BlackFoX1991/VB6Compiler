namespace VB6.Syntax.Nodes;

/// <summary>
/// Finds the token a syntax node starts at.
///
/// Only tokens carry a <see cref="Text.TextSpan"/>, so this is how a bound node gets the source
/// position that later becomes a debugger sequence point. The mapping is an explicit switch rather
/// than a reflective walk: which token opens a construct is a language decision - <c>Call Foo</c>
/// starts at <c>Call</c>, <c>point.X = 1</c> starts inside its target expression - and a new node
/// type should force that decision to be made rather than silently pick the first field.
/// </summary>
public static class SyntaxNavigator
{
    /// <summary>
    /// The token a statement begins at, or <see langword="null"/> for a statement synthesized by
    /// a lowering pass, which has no source of its own.
    /// </summary>
    public static SyntaxToken? GetFirstToken(StatementSyntax statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        return statement switch
        {
            DimStatementSyntax dim => dim.DimKeyword,
            ConstStatementSyntax constant => constant.ConstKeyword,
            StaticStatementSyntax @static => @static.StaticKeyword,
            ReDimStatementSyntax reDim => reDim.ReDimKeyword,
            EraseStatementSyntax erase => erase.EraseKeyword,
            AssignmentStatementSyntax assignment => assignment.Identifier,
            ArrayElementAssignmentStatementSyntax assignment => assignment.Identifier,
            MemberAssignmentStatementSyntax assignment => GetFirstToken(assignment.Target),
            IfStatementSyntax @if => @if.IfKeyword,
            ForStatementSyntax @for => @for.ForKeyword,
            ForEachStatementSyntax forEach => forEach.ForKeyword,
            WhileStatementSyntax @while => @while.WhileKeyword,
            DoStatementSyntax @do => @do.DoKeyword,
            ExitStatementSyntax exit => exit.ExitKeyword,
            SelectCaseStatementSyntax select => select.SelectKeyword,
            WithStatementSyntax with => with.WithKeyword,
            DebugPrintStatementSyntax debugPrint => debugPrint.DebugKeyword,
            DebugAssertStatementSyntax debugAssert => debugAssert.DebugKeyword,
            ErrorStatementSyntax errorStatement => errorStatement.ErrorKeyword,
            FilePrintStatementSyntax filePrint => filePrint.PrintKeyword,
            FileWriteStatementSyntax fileWrite => fileWrite.WriteKeyword,
            LockStatementSyntax lockStatement => lockStatement.LockKeyword,
            UnlockStatementSyntax unlockStatement => unlockStatement.UnlockKeyword,
            InvocationStatementSyntax invocation => invocation.CallKeyword ?? invocation.Identifier,
            QualifiedInvocationStatementSyntax invocation => GetFirstToken(invocation.Target),
            GoToStatementSyntax @goto => @goto.GoToKeyword,
            GoSubStatementSyntax goSub => goSub.GoSubKeyword,
            GoSubReturnStatementSyntax goSubReturn => goSubReturn.ReturnKeyword,
            LabelStatementSyntax label => label.Identifier,
            OnGoToStatementSyntax onGoTo => GetFirstToken(onGoTo.Expression),
            OnGoSubStatementSyntax onGoSub => GetFirstToken(onGoSub.Expression),
            OnErrorStatementSyntax onError => onError.OnKeyword,
            ResumeStatementSyntax resume => resume.ResumeKeyword,
            OpenStatementSyntax open => open.OpenKeyword,
            NameStatementSyntax name => name.NameKeyword,
            CloseStatementSyntax close => close.CloseKeyword,
            GetStatementSyntax get => get.GetKeyword,
            PutStatementSyntax put => put.PutKeyword,
            SeekStatementSyntax seek => seek.SeekKeyword,
            LineInputStatementSyntax lineInput => lineInput.LineKeyword,
            FileInputStatementSyntax fileInput => fileInput.InputKeyword,
            WidthStatementSyntax width => width.WidthKeyword,
            LineStatementSyntax line => line.Target is null
                ? line.LineKeyword
                : GetFirstToken(line.Target),
            EndStatementSyntax end => end.EndKeyword,
            SkippedStatementSyntax skipped => skipped.Token,
            _ => null
        };
    }

    /// <summary>The token an expression begins at, following its leftmost operand.</summary>
    public static SyntaxToken? GetFirstToken(ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.LiteralToken,
            NameExpressionSyntax name => name.IdentifierToken,
            AddressOfExpressionSyntax addressOf => addressOf.AddressOfKeyword,
            InvocationExpressionSyntax invocation => invocation.Identifier,
            ElementAccessExpressionSyntax element => GetFirstToken(element.Receiver),
            MemberAccessExpressionSyntax member => GetFirstToken(member.Receiver),
            UnaryExpressionSyntax unary => unary.OperatorToken,
            BinaryExpressionSyntax binary => GetFirstToken(binary.Left),
            ParenthesizedExpressionSyntax parenthesized => parenthesized.OpenParenthesisToken,
            ArgumentPassingModeExpressionSyntax passingMode => passingMode.PassingModeKeyword,
            NamedArgumentExpressionSyntax named => named.NameToken,
            TypeOfExpressionSyntax typeOf => typeOf.TypeOfKeyword,

            // A With receiver and an omitted argument are positions in the source, not text.
            _ => null
        };
    }
}
