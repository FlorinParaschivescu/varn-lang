using Varn.Syntax;

namespace Varn.Lexer;

public enum TokenKind
{
    EndOfFile,
    NewLine,
    Identifier,
    Slot,
    Integer,
    Float,
    String,
    Fn,
    Let,
    Var,
    Set,
    Ret,
    End,
    Cap,
    Budget,
    If,
    Else,
    Loop,
    Each,
    In,
    From,
    To,
    Max,
    True,
    False,
    Null,
    Some,
    None,
    List,
    Rec,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Comma,
    Colon,
    Arrow,
    Bang,
    Equals,
    Question,
    Dot
}

public sealed record Token(TokenKind Kind, string Text, SourceSpan Span);

public sealed record LexResult(IReadOnlyList<Token> Tokens, IReadOnlyList<Diagnostic> Diagnostics);
