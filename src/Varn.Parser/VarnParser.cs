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
            var records = new List<RecordSyntax>();
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
                    case TokenKind.Rec:
                        records.Add(ParseRecord());
                        break;
                    case TokenKind.Fn:
                        functions.Add(ParseFunction());
                        break;
                    default:
                        Report("VARN2000", $"Expected 'cap', 'budget', 'rec', or 'fn', but found '{Current.Text}'.", Current.Span);
                        MoveNext();
                        break;
                }

                SkipNewLines();
            }

            return new ParseResult(
                new ProgramSyntax(capabilities, stepBudget, records, functions, new SourceSpan(1, 1)),
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

        private RecordSyntax ParseRecord()
        {
            var start = Match(TokenKind.Rec).Span;
            var name = MatchSimpleName("record");
            Match(TokenKind.LeftParen);
            var fields = new List<RecordFieldSyntax>();
            if (Current.Kind != TokenKind.RightParen)
            {
                do
                {
                    var fieldName = MatchSimpleName("field");
                    Match(TokenKind.Colon);
                    var type = ParseType();
                    fields.Add(new RecordFieldSyntax(fieldName.Text, type, fieldName.Span));
                    if (Current.Kind != TokenKind.Comma)
                    {
                        break;
                    }

                    MoveNext();
                }
                while (Current.Kind != TokenKind.EndOfFile);
            }

            Match(TokenKind.RightParen);
            RequireLineEnd();
            return new RecordSyntax(name.Text, fields, start);
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
                    var parameterName = MatchSimpleName("parameter");
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

        private static bool IsContextualKeyword(TokenKind kind) =>
            kind is TokenKind.Max or TokenKind.From or TokenKind.To or TokenKind.In;

        private StatementSyntax ParseStatement()
        {
            return Current.Kind switch
            {
                TokenKind.Let => ParseLetStatement(),
                TokenKind.Var => ParseVarStatement(),
                TokenKind.Set => ParseSetStatement(),
                TokenKind.Ret => ParseReturnStatement(),
                TokenKind.If => ParseIfStatement(),
                TokenKind.Loop => ParseLoopStatement(),
                TokenKind.Each => ParseEachStatement(),
                _ => ParseExpressionStatement()
            };
        }

        private LetStatementSyntax ParseLetStatement()
        {
            var start = Match(TokenKind.Let).Span;
            var name = MatchSimpleName("binding").Text;
            Match(TokenKind.Colon);
            var type = ParseType();
            var value = ParseExpression();
            return new LetStatementSyntax(name, type, value, start);
        }

        private VarStatementSyntax ParseVarStatement()
        {
            var start = Match(TokenKind.Var).Span;
            var name = MatchSimpleName("binding").Text;
            Match(TokenKind.Colon);
            var type = ParseType();
            var value = ParseExpression();
            return new VarStatementSyntax(name, type, value, start);
        }

        private SetStatementSyntax ParseSetStatement()
        {
            var start = Match(TokenKind.Set).Span;
            var name = MatchSimpleName("binding").Text;
            var value = ParseExpression();
            return new SetStatementSyntax(name, value, start);
        }

        private ReturnStatementSyntax ParseReturnStatement()
        {
            var start = Match(TokenKind.Ret).Span;
            return new ReturnStatementSyntax(ParseExpression(), start);
        }

        private StatementSyntax ParseIfStatement()
        {
            var start = Match(TokenKind.If).Span;
            if (Current.Kind == TokenKind.Let)
            {
                return ParseIfLetStatement(start);
            }

            if (Current.Kind == TokenKind.Ok)
            {
                return ParseIfOkStatement(start);
            }

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

        private IfLetStatementSyntax ParseIfLetStatement(SourceSpan start)
        {
            Match(TokenKind.Let);
            var binding = MatchSimpleName("binding").Text;
            Match(TokenKind.Colon);
            var bindingType = ParseType();
            var optional = ParseExpression();
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
            return new IfLetStatementSyntax(binding, bindingType, optional, thenBody, elseBody, start);
        }

        private IfOkStatementSyntax ParseIfOkStatement(SourceSpan start)
        {
            Match(TokenKind.Ok);
            var binding = MatchSimpleName("binding").Text;
            Match(TokenKind.Colon);
            var bindingType = ParseType();
            var result = ParseExpression();
            RequireLineEnd();
            SkipNewLines();
            var thenBody = ParseBlock(TokenKind.Else, TokenKind.End);
            string? errorBinding = null;
            IReadOnlyList<StatementSyntax> elseBody = [];
            if (Current.Kind == TokenKind.Else)
            {
                MoveNext();
                if (Current.Kind == TokenKind.Err)
                {
                    MoveNext();
                    errorBinding = MatchSimpleName("binding").Text;
                    Match(TokenKind.Colon);
                    _ = ParseType();
                }

                RequireLineEnd();
                SkipNewLines();
                elseBody = ParseBlock(TokenKind.End);
            }

            Match(TokenKind.End);
            return new IfOkStatementSyntax(binding, bindingType, result, thenBody, errorBinding, elseBody, start);
        }

        private LoopStatementSyntax ParseLoopStatement()
        {
            var start = Match(TokenKind.Loop).Span;
            var iterator = MatchSimpleName("binding").Text;
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

        private EachStatementSyntax ParseEachStatement()
        {
            var start = Match(TokenKind.Each).Span;
            var iterator = MatchSimpleName("binding").Text;
            Match(TokenKind.Colon);
            var iteratorType = ParseType();
            Match(TokenKind.In);
            var list = ParseExpression();
            Match(TokenKind.Max);
            var maxIterations = ParseI64Literal();
            RequireLineEnd();
            SkipNewLines();
            var body = ParseBlock(TokenKind.End);
            Match(TokenKind.End);
            return new EachStatementSyntax(iterator, iteratorType, list, maxIterations, body, start);
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
            var expression = ParsePrimaryExpression();
            while (Current.Kind == TokenKind.Dot)
            {
                MoveNext();
                // The lexer folds dots into identifiers so module names like io.print stay one
                // token, which makes a chained access such as @0.home.city arrive as a single
                // "home.city" identifier. Field names may not contain dots, so splitting here is
                // unambiguous and rebuilds one access per segment.
                var field = MatchName();
                foreach (var segment in field.Text.Split('.'))
                {
                    if (segment.Length == 0)
                    {
                        Report("VARN2007", "A field name must not be empty.", field.Span);
                        continue;
                    }

                    expression = new FieldExpressionSyntax(expression, segment, field.Span);
                }
            }

            return expression;
        }

        private ExpressionSyntax ParsePrimaryExpression()
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
                case TokenKind.Some:
                    return ParseSomeExpression();
                case TokenKind.None:
                    return ParseNoneExpression();
                case TokenKind.List:
                    return ParseListExpression();
                case TokenKind.Ok:
                    return ParseOkExpression();
                case TokenKind.Err:
                    return ParseErrExpression();
                case TokenKind.Rec:
                    return ParseRecordExpression();
                case TokenKind.Identifier:
                    return Peek.Kind == TokenKind.LeftParen ? ParseCall() : ParseReference();
                case var contextual when IsContextualKeyword(contextual):
                    return Peek.Kind == TokenKind.LeftParen ? ParseCall() : ParseReference();
                default:
                    Report("VARN2004", $"Expected an expression, but found '{token.Text}'.", token.Span);
                    MoveNext();
                    return new LiteralExpressionSyntax(null, VarnType.Null, token.Span);
            }
        }

        private SomeExpressionSyntax ParseSomeExpression()
        {
            var start = Match(TokenKind.Some).Span;
            Match(TokenKind.LeftParen);
            var value = ParseExpression();
            Match(TokenKind.RightParen);
            return new SomeExpressionSyntax(value, start);
        }

        private NoneExpressionSyntax ParseNoneExpression()
        {
            var start = Match(TokenKind.None).Span;
            Match(TokenKind.LeftBracket);
            var elementType = ParseType();
            Match(TokenKind.RightBracket);
            return new NoneExpressionSyntax(elementType, start);
        }

        private OkExpressionSyntax ParseOkExpression()
        {
            var start = Match(TokenKind.Ok).Span;
            Match(TokenKind.LeftParen);
            var value = ParseExpression();
            Match(TokenKind.RightParen);
            return new OkExpressionSyntax(value, start);
        }

        private ErrExpressionSyntax ParseErrExpression()
        {
            var start = Match(TokenKind.Err).Span;
            Match(TokenKind.LeftBracket);
            var valueType = ParseType();
            Match(TokenKind.RightBracket);
            Match(TokenKind.LeftParen);
            var error = ParseExpression();
            Match(TokenKind.RightParen);
            return new ErrExpressionSyntax(valueType, error, start);
        }

        private ListExpressionSyntax ParseListExpression()
        {
            var start = Match(TokenKind.List).Span;
            Match(TokenKind.LeftBracket);
            var elementType = ParseType();
            Match(TokenKind.RightBracket);
            Match(TokenKind.LeftParen);
            var elements = new List<ExpressionSyntax>();
            if (Current.Kind != TokenKind.RightParen)
            {
                do
                {
                    elements.Add(ParseExpression());
                    if (Current.Kind != TokenKind.Comma)
                    {
                        break;
                    }

                    MoveNext();
                }
                while (Current.Kind != TokenKind.EndOfFile);
            }

            Match(TokenKind.RightParen);
            return new ListExpressionSyntax(elementType, elements, start);
        }

        private RecordExpressionSyntax ParseRecordExpression()
        {
            var start = Match(TokenKind.Rec).Span;
            Match(TokenKind.LeftBracket);
            var typeName = MatchSimpleName("record");
            Match(TokenKind.RightBracket);
            Match(TokenKind.LeftParen);
            var fields = new List<RecordInitializerSyntax>();
            if (Current.Kind != TokenKind.RightParen)
            {
                do
                {
                    var fieldName = MatchSimpleName("field");
                    Match(TokenKind.Equals);
                    fields.Add(new RecordInitializerSyntax(fieldName.Text, ParseExpression(), fieldName.Span));
                    if (Current.Kind != TokenKind.Comma)
                    {
                        break;
                    }

                    MoveNext();
                }
                while (Current.Kind != TokenKind.EndOfFile);
            }

            Match(TokenKind.RightParen);
            return new RecordExpressionSyntax(typeName.Text, fields, start);
        }

        private ExpressionSyntax ParseReference()
        {
            // The lexer folds dots into identifiers so module names like io.print stay one token,
            // which makes a binding access such as order.items arrive as a single "order.items"
            // identifier. The first segment names the binding and every later one is a field.
            var token = MatchName();
            var segments = token.Text.Split('.');
            ExpressionSyntax expression = new ReferenceExpressionSyntax(segments[0], token.Span);
            foreach (var segment in segments[1..])
            {
                if (segment.Length == 0)
                {
                    Report("VARN2007", "A field name must not be empty.", token.Span);
                    continue;
                }

                expression = new FieldExpressionSyntax(expression, segment, token.Span);
            }

            return expression;
        }

        private CallExpressionSyntax ParseCall()
        {
            var name = MatchName();
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
            VarnType type;
            if (Current.Kind is TokenKind.List or TokenKind.Result)
            {
                var constructor = TakeCurrent().Kind;
                Match(TokenKind.LeftBracket);
                var argumentType = ParseType();
                Match(TokenKind.RightBracket);
                type = constructor == TokenKind.List
                    ? VarnType.List(argumentType)
                    : VarnType.Result(argumentType);
            }
            else
            {
                var token = Current.Kind == TokenKind.Null ? TakeCurrent() : Match(TokenKind.Identifier);
                type = VarnType.Parse(token.Text);
            }
            while (Current.Kind == TokenKind.Question)
            {
                MoveNext();
                type = VarnType.Optional(type);
            }

            return type;
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

        private Token TakeCurrent()
        {
            var token = Current;
            MoveNext();
            return token;
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

        private Token MatchName() =>
            IsContextualKeyword(Current.Kind) ? TakeCurrent() : Match(TokenKind.Identifier);

        private Token MatchSimpleName(string kind)
        {
            var token = MatchName();
            if (token.Text.Contains('.', StringComparison.Ordinal))
            {
                Report("VARN2007", $"A {kind} name must not contain '.'.", token.Span);
            }

            return token;
        }

        private long? ReportAndReturnNull(string code, string message, SourceSpan span)
        {
            Report(code, message, span);
            return null;
        }

        private void Report(string code, string message, SourceSpan span) =>
            _diagnostics.Add(new Diagnostic(code, message, span));

        private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];

        private Token Peek => _tokens[Math.Min(_position + 1, _tokens.Count - 1)];

        private void MoveNext()
        {
            if (_position < _tokens.Count - 1)
            {
                _position++;
            }
        }
    }
}
