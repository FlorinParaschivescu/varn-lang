using System.Globalization;
using System.Text;
using Varn.Syntax;

namespace Varn.Lexer;

public static class VarnLexer
{
    private static readonly IReadOnlyDictionary<string, TokenKind> Keywords =
        new Dictionary<string, TokenKind>(StringComparer.Ordinal)
        {
            ["fn"] = TokenKind.Fn,
            ["let"] = TokenKind.Let,
            ["ret"] = TokenKind.Ret,
            ["end"] = TokenKind.End,
            ["cap"] = TokenKind.Cap,
            ["budget"] = TokenKind.Budget,
            ["if"] = TokenKind.If,
            ["else"] = TokenKind.Else,
            ["loop"] = TokenKind.Loop,
            ["from"] = TokenKind.From,
            ["to"] = TokenKind.To,
            ["max"] = TokenKind.Max,
            ["true"] = TokenKind.True,
            ["false"] = TokenKind.False,
            ["null"] = TokenKind.Null
        };

    public static LexResult Lex(string source)
    {
        var tokens = new List<Token>();
        var diagnostics = new List<Diagnostic>();
        var position = 0;
        var line = 1;
        var column = 1;

        while (position < source.Length)
        {
            var current = source[position];
            var span = new SourceSpan(line, column);

            if (current is ' ' or '\t' or '\r')
            {
                position++;
                column++;
                continue;
            }

            if (current == '\n')
            {
                tokens.Add(new Token(TokenKind.NewLine, "\n", span));
                position++;
                line++;
                column = 1;
                continue;
            }

            if (current == '#')
            {
                while (position < source.Length && source[position] != '\n')
                {
                    position++;
                    column++;
                }

                continue;
            }

            if (current == '@')
            {
                var start = position++;
                column++;
                while (position < source.Length && char.IsAsciiDigit(source[position]))
                {
                    position++;
                    column++;
                }

                if (position == start + 1)
                {
                    diagnostics.Add(new Diagnostic("VARN1001", "A slot must contain a numeric id after '@'.", span));
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Slot, source[start..position], span));
                }

                continue;
            }

            if (char.IsAsciiLetter(current) || current == '_')
            {
                var start = position;
                while (position < source.Length && IsIdentifierCharacter(source[position]))
                {
                    position++;
                    column++;
                }

                var text = source[start..position];
                tokens.Add(new Token(Keywords.GetValueOrDefault(text, TokenKind.Identifier), text, span));
                continue;
            }

            if (char.IsAsciiDigit(current) || (current == '-' && position + 1 < source.Length && char.IsAsciiDigit(source[position + 1])))
            {
                var start = position;
                if (source[position] == '-')
                {
                    position++;
                    column++;
                }

                while (position < source.Length && char.IsAsciiDigit(source[position]))
                {
                    position++;
                    column++;
                }

                var kind = TokenKind.Integer;
                if (position < source.Length && source[position] == '.')
                {
                    kind = TokenKind.Float;
                    position++;
                    column++;
                    while (position < source.Length && char.IsAsciiDigit(source[position]))
                    {
                        position++;
                        column++;
                    }
                }

                var text = source[start..position];
                var valid = kind == TokenKind.Integer
                    ? long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    : double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                if (!valid)
                {
                    diagnostics.Add(new Diagnostic("VARN1002", $"Invalid {kind.ToString().ToLowerInvariant()} literal '{text}'.", span));
                }

                tokens.Add(new Token(kind, text, span));
                continue;
            }

            if (current == '"')
            {
                var builder = new StringBuilder();
                position++;
                column++;
                var terminated = false;
                while (position < source.Length && source[position] != '\n')
                {
                    current = source[position++];
                    column++;
                    if (current == '"')
                    {
                        terminated = true;
                        break;
                    }

                    if (current == '\\' && position < source.Length)
                    {
                        var escaped = source[position++];
                        column++;
                        builder.Append(escaped switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '"' => '"',
                            '\\' => '\\',
                            _ => escaped
                        });
                    }
                    else
                    {
                        builder.Append(current);
                    }
                }

                if (!terminated)
                {
                    diagnostics.Add(new Diagnostic("VARN1003", "Unterminated string literal.", span));
                }

                tokens.Add(new Token(TokenKind.String, builder.ToString(), span));
                continue;
            }

            if (current == '-' && position + 1 < source.Length && source[position + 1] == '>')
            {
                tokens.Add(new Token(TokenKind.Arrow, "->", span));
                position += 2;
                column += 2;
                continue;
            }

            var punctuation = current switch
            {
                '(' => TokenKind.LeftParen,
                ')' => TokenKind.RightParen,
                '[' => TokenKind.LeftBracket,
                ']' => TokenKind.RightBracket,
                ',' => TokenKind.Comma,
                ':' => TokenKind.Colon,
                '!' => TokenKind.Bang,
                '=' => TokenKind.Equals,
                _ => (TokenKind?)null
            };

            if (punctuation is { } punctuationKind)
            {
                tokens.Add(new Token(punctuationKind, current.ToString(), span));
            }
            else
            {
                diagnostics.Add(new Diagnostic("VARN1000", $"Unexpected character '{current}'.", span));
            }

            position++;
            column++;
        }

        tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, new SourceSpan(line, column)));
        return new LexResult(tokens, diagnostics);
    }

    private static bool IsIdentifierCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '_' or '.';
}
