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
    Ret,
    End,
    Cap,
    Budget,
    If,
    Else,
    Loop,
    From,
    To,
    Max,
    True,
    False,
    Null,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Comma,
    Colon,
    Arrow,
    Bang,
    Equals
}

public sealed record Token(TokenKind Kind, string Text, SourceSpan Span);

public sealed record LexResult(IReadOnlyList<Token> Tokens, IReadOnlyList<Diagnostic> Diagnostics);
