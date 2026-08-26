using System.Globalization;
using Varn.Lexer;
using Varn.Syntax;

namespace Varn.Parser;

public static class VarnParser
{
    public static ParseResult Parse(string source)
    {
        var lexed = VarnLexer.Lex(source);
        var parser = new ParserState(lexed.Tokens, lexed.Diagnostics);
        return parser.ParseProgram();
    }

    private sealed class ParserState
    {
        private readonly IReadOnlyList<Token> _tokens;
        private readonly List<Diagnostic> _diagnostics;
        private int _position;

        public ParserState(IReadOnlyList<Token> tokens, IReadOnlyList<Diagnostic> diagnostics)
        {
            _tokens = tokens;
            _diagnostics = [.. diagnostics];
        }

        public ParseResult ParseProgram()
        {
            var capabilities = new List<string>();
            long? stepBudget = null;
            var functions = new List<FunctionSyntax>();
            SkipNewLines();

            while (Current.Kind != TokenKind.EndOfFile)
            {
                switch (Current.Kind)
                {
                    case TokenKind.Cap:
                        capabilities.AddRange(ParseCapabilityDirective());
                        break;
                    case TokenKind.Budget:
                        var parsedBudget = ParseBudgetDirective();
                        if (stepBudget is not null)
                        {
                            Report("VARN2002", "The step budget may be declared only once.", Current.Span);
                        }

                        stepBudget = parsedBudget;
                        break;
                    case TokenKind.Fn:
                        functions.Add(ParseFunction());
                        break;
                    default:
                        Report("VARN2000", $"Expected 'cap', 'budget', or 'fn', but found '{Current.Text}'.", Current.Span);
                        MoveNext();
                        break;
                }

                SkipNewLines();
            }

            return new ParseResult(
                new ProgramSyntax(capabilities, stepBudget, functions, new SourceSpan(1, 1)),
                _diagnostics);
        }

        private IReadOnlyList<string> ParseCapabilityDirective()
        {
            Match(TokenKind.Cap);
            Match(TokenKind.LeftBracket);
            var capabilities = ParseNameList();
            Match(TokenKind.RightBracket);
            RequireLineEnd();
            return capabilities;
        }

        private long? ParseBudgetDirective()
        {
            var start = Match(TokenKind.Budget).Span;
            Match(TokenKind.LeftBracket);
            var name = Match(TokenKind.Identifier);
            if (name.Text != "steps")
            {
                Report("VARN2001", "v0.1 supports only the 'steps' resource budget.", name.Span);
            }

            Match(TokenKind.Equals);
            var value = Match(TokenKind.Integer);
            Match(TokenKind.RightBracket);
            RequireLineEnd();
            return long.TryParse(value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : ReportAndReturnNull("VARN2003", "The step budget must be a valid i64 value.", start);
        }

        private FunctionSyntax ParseFunction()
        {
            var start = Match(TokenKind.Fn).Span;
            var name = Match(TokenKind.Identifier).Text;
            Match(TokenKind.LeftParen);
            var parameters = new List<ParameterSyntax>();
            if (Current.Kind != TokenKind.RightParen)
            {
                do
                {
                    var parameterName = Match(TokenKind.Slot);
                    Match(TokenKind.Colon);
                    var type = ParseType();
                    parameters.Add(new ParameterSyntax(parameterName.Text, type, parameterName.Span));
                    if (Current.Kind != TokenKind.Comma)
                    {
                        break;
                    }

                    MoveNext();
                }
                while (Current.Kind != TokenKind.EndOfFile);
            }

            Match(TokenKind.RightParen);
            Match(TokenKind.Arrow);
            var returnType = ParseType();
            var effects = new List<string>();
            if (Current.Kind == TokenKind.Bang)
            {
                MoveNext();
                Match(TokenKind.LeftBracket);
                effects.AddRange(ParseNameList());
                Match(TokenKind.RightBracket);
            }

            RequireLineEnd();
            SkipNewLines();
            var body = ParseBlock(TokenKind.End);
            Match(TokenKind.End);
            RequireLineEnd();
            return new FunctionSyntax(name, parameters, returnType, effects, body, start);
        }

        private IReadOnlyList<StatementSyntax> ParseBlock(params TokenKind[] terminators)
        {
            var statements = new List<StatementSyntax>();
            while (Current.Kind != TokenKind.EndOfFile && !terminators.Contains(Current.Kind))
            {
                statements.Add(ParseStatement());
                RequireLineEnd();
                SkipNewLines();
            }

            return statements;
        }

        private StatementSyntax ParseStatement()
        {
            return Current.Kind switch
            {
                TokenKind.Let => ParseLetStatement(),
                TokenKind.Ret => ParseReturnStatement(),
                TokenKind.If => ParseIfStatement(),
                TokenKind.Loop => ParseLoopStatement(),
                _ => ParseExpressionStatement()
            };
        }

        private LetStatementSyntax ParseLetStatement()
        {
            var start = Match(TokenKind.Let).Span;
            var name = Match(TokenKind.Slot).Text;
            Match(TokenKind.Colon);
            var type = ParseType();
            var value = ParseExpression();
            return new LetStatementSyntax(name, type, value, start);
        }

        private ReturnStatementSyntax ParseReturnStatement()
        {
            var start = Match(TokenKind.Ret).Span;
            return new ReturnStatementSyntax(ParseExpression(), start);
        }

        private IfStatementSyntax ParseIfStatement()
        {
            var start = Match(TokenKind.If).Span;
            var condition = ParseExpression();
            RequireLineEnd();
            SkipNewLines();
            var thenBody = ParseBlock(TokenKind.Else, TokenKind.End);
            IReadOnlyList<StatementSyntax> elseBody = [];
            if (Current.Kind == TokenKind.Else)
            {
                MoveNext();
                RequireLineEnd();
                SkipNewLines();
                elseBody = ParseBlock(TokenKind.End);
            }

            Match(TokenKind.End);
            return new IfStatementSyntax(condition, thenBody, elseBody, start);
        }

        private LoopStatementSyntax ParseLoopStatement()
        {
            var start = Match(TokenKind.Loop).Span;
            var iterator = Match(TokenKind.Slot).Text;
            Match(TokenKind.Colon);
            var iteratorType = ParseType();
            Match(TokenKind.From);
            var startInclusive = ParseI64Literal();
            Match(TokenKind.To);
            var endExclusive = ParseI64Literal();
            Match(TokenKind.Max);
            var maxIterations = ParseI64Literal();
            RequireLineEnd();
            SkipNewLines();
            var body = ParseBlock(TokenKind.End);
            Match(TokenKind.End);
            return new LoopStatementSyntax(
                iterator,
                iteratorType,
                startInclusive,
                endExclusive,
                maxIterations,
                body,
                start);
        }

        private long ParseI64Literal()
        {
            var token = Match(TokenKind.Integer);
            if (long.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            Report("VARN2006", "A loop bound must be a valid i64 literal.", token.Span);
            return 0;
        }

        private ExpressionStatementSyntax ParseExpressionStatement()
        {
            var expression = ParseExpression();
            return new ExpressionStatementSyntax(expression, expression.Span);
        }

        private ExpressionSyntax ParseExpression()
        {
            var token = Current;
            switch (token.Kind)
            {
                case TokenKind.Integer:
                    MoveNext();
                    _ = long.TryParse(token.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer);
                    return new LiteralExpressionSyntax(integer, VarnType.I64, token.Span);
                case TokenKind.Float:
                    MoveNext();
                    _ = double.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating);
                    return new LiteralExpressionSyntax(floating, VarnType.F64, token.Span);
                case TokenKind.String:
                    MoveNext();
                    return new LiteralExpressionSyntax(token.Text, VarnType.String, token.Span);
                case TokenKind.True:
                case TokenKind.False:
                    MoveNext();
                    return new LiteralExpressionSyntax(token.Kind == TokenKind.True, VarnType.Bool, token.Span);
                case TokenKind.Null:
                    MoveNext();
                    return new LiteralExpressionSyntax(null, VarnType.Null, token.Span);
                case TokenKind.Slot:
                    MoveNext();
                    return new ReferenceExpressionSyntax(token.Text, token.Span);
                case TokenKind.Identifier:
                    return ParseCall();
                default:
                    Report("VARN2004", $"Expected an expression, but found '{token.Text}'.", token.Span);
                    MoveNext();
                    return new LiteralExpressionSyntax(null, VarnType.Null, token.Span);
            }
        }

        private CallExpressionSyntax ParseCall()
        {
            var name = Match(TokenKind.Identifier);
            Match(TokenKind.LeftParen);
            var arguments = new List<ExpressionSyntax>();
            if (Current.Kind != TokenKind.RightParen)
            {
                do
                {
                    arguments.Add(ParseExpression());
                    if (Current.Kind != TokenKind.Comma)
                    {
                        break;
                    }

                    MoveNext();
                }
                while (Current.Kind != TokenKind.EndOfFile);
            }

            Match(TokenKind.RightParen);
            return new CallExpressionSyntax(name.Text, arguments, name.Span);
        }

        private VarnType ParseType()
        {
            var token = Match(TokenKind.Identifier);
            return VarnType.Parse(token.Text);
        }

        private IReadOnlyList<string> ParseNameList()
        {
            var names = new List<string>();
            if (Current.Kind == TokenKind.RightBracket)
            {
                return names;
            }

            do
            {
                names.Add(Match(TokenKind.Identifier).Text);
                if (Current.Kind != TokenKind.Comma)
                {
                    break;
                }

                MoveNext();
            }
            while (Current.Kind != TokenKind.EndOfFile);
            return names;
        }

        private void RequireLineEnd()
        {
            if (Current.Kind is TokenKind.NewLine or TokenKind.EndOfFile)
            {
                return;
            }

            Report("VARN2005", $"Expected the end of the line, but found '{Current.Text}'.", Current.Span);
            while (Current.Kind is not TokenKind.NewLine and not TokenKind.EndOfFile)
            {
                MoveNext();
            }
        }

        private void SkipNewLines()
        {
            while (Current.Kind == TokenKind.NewLine)
            {
                MoveNext();
            }
        }

        private Token Match(TokenKind expected)
        {
            if (Current.Kind == expected)
            {
                var token = Current;
                MoveNext();
                return token;
            }

            Report("VARN2099", $"Expected {expected}, but found {Current.Kind} ('{Current.Text}').", Current.Span);
            return new Token(expected, string.Empty, Current.Span);
        }

        private long? ReportAndReturnNull(string code, string message, SourceSpan span)
        {
            Report(code, message, span);
            return null;
        }

        private void Report(string code, string message, SourceSpan span) =>
            _diagnostics.Add(new Diagnostic(code, message, span));

        private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];

        private void MoveNext()
        {
            if (_position < _tokens.Count - 1)
            {
                _position++;
            }
        }
    }
}
