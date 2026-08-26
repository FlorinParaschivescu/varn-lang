using System.Globalization;
using System.Text;
using System.Text.Json;
using Varn.Syntax;

namespace Varn.Runtime;

public static class CanonicalFormatter
{
    public static string Format(ProgramSyntax program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new Writer(program).Format();
    }

    private sealed class Writer
    {
        private readonly StringBuilder _builder = new();
        private readonly ProgramSyntax _program;
        private readonly IReadOnlyDictionary<string, RecordSyntax> _records;

        public Writer(ProgramSyntax program)
        {
            _program = program;
            var records = new Dictionary<string, RecordSyntax>(StringComparer.Ordinal);
            foreach (var record in program.Records)
            {
                records[record.Name] = record;
            }

            _records = records;
        }

        public string Format()
        {
            _builder.Append("P{C[");
            AppendJoined(_program.Capabilities.Order(StringComparer.Ordinal), capability => _builder.Append(capability));
            _builder.Append("];B[")
                .Append(_program.StepBudget?.ToString(CultureInfo.InvariantCulture) ?? "?")
                .Append("];T[");
            AppendJoined(_program.Records.OrderBy(static record => record.Name, StringComparer.Ordinal), AppendRecord);
            _builder.Append("];F[");
            AppendJoined(_program.Functions, AppendFunction);
            return _builder.Append("]}").ToString();
        }

        private void AppendRecord(RecordSyntax record)
        {
            _builder.Append(record.Name).Append('(');
            AppendJoined(record.Fields, field => _builder.Append(field.Name).Append(':').Append(field.Type.Name));
            _builder.Append(')');
        }

        private void AppendFunction(FunctionSyntax function)
        {
            _builder.Append(function.Name).Append('(');
            AppendJoined(function.Parameters, parameter =>
                _builder.Append(parameter.Name).Append(':').Append(parameter.Type.Name));
            _builder.Append(")->").Append(function.ReturnType.Name).Append("![");
            AppendJoined(function.Effects.Order(StringComparer.Ordinal), effect => _builder.Append(effect));
            _builder.Append("]{");
            AppendJoined(function.Body, AppendStatement);
            _builder.Append('}');
        }

        private void AppendStatement(StatementSyntax statement)
        {
            switch (statement)
            {
                case LetStatementSyntax let:
                    _builder.Append("L(").Append(let.Name).Append(':').Append(let.Type.Name).Append(',');
                    AppendExpression(let.Value);
                    _builder.Append(')');
                    break;
                case VarStatementSyntax variable:
                    _builder.Append("M(").Append(variable.Name).Append(':').Append(variable.Type.Name).Append(',');
                    AppendExpression(variable.Value);
                    _builder.Append(')');
                    break;
                case SetStatementSyntax assignment:
                    _builder.Append("S(").Append(assignment.Name).Append(',');
                    AppendExpression(assignment.Value);
                    _builder.Append(')');
                    break;
                case ExpressionStatementSyntax expressionStatement:
                    _builder.Append("E(");
                    AppendExpression(expressionStatement.Expression);
                    _builder.Append(')');
                    break;
                case ReturnStatementSyntax returnStatement:
                    _builder.Append("R(");
                    AppendExpression(returnStatement.Value);
                    _builder.Append(')');
                    break;
                case IfStatementSyntax conditional:
                    _builder.Append("I(");
                    AppendExpression(conditional.Condition);
                    _builder.Append("){T{");
                    AppendJoined(conditional.ThenBody, AppendStatement);
                    _builder.Append("};E{");
                    AppendJoined(conditional.ElseBody, AppendStatement);
                    _builder.Append("}}");
                    break;
                case IfLetStatementSyntax ifLet:
                    _builder.Append("J(").Append(ifLet.Binding).Append(':').Append(ifLet.BindingType.Name).Append(',');
                    AppendExpression(ifLet.Optional);
                    _builder.Append("){T{");
                    AppendJoined(ifLet.ThenBody, AppendStatement);
                    _builder.Append("};E{");
                    AppendJoined(ifLet.ElseBody, AppendStatement);
                    _builder.Append("}}");
                    break;
                case IfOkStatementSyntax ifOk:
                    _builder.Append("U(").Append(ifOk.Binding).Append(':').Append(ifOk.BindingType.Name).Append(',');
                    AppendExpression(ifOk.Result);
                    _builder.Append("){T{");
                    AppendJoined(ifOk.ThenBody, AppendStatement);
                    _builder.Append("};E[").Append(ifOk.ErrorBinding ?? string.Empty).Append("]{");
                    AppendJoined(ifOk.ElseBody, AppendStatement);
                    _builder.Append("}}");
                    break;
                case LoopStatementSyntax loop:
                    _builder.Append("O(")
                        .Append(loop.Iterator).Append(':').Append(loop.IteratorType.Name).Append(',')
                        .Append(loop.StartInclusive.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(loop.EndExclusive.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(loop.MaxIterations.ToString(CultureInfo.InvariantCulture)).Append("){");
                    AppendJoined(loop.Body, AppendStatement);
                    _builder.Append('}');
                    break;
                case EachStatementSyntax each:
                    _builder.Append("H(").Append(each.Iterator).Append(':').Append(each.IteratorType.Name).Append(',');
                    AppendExpression(each.List);
                    _builder.Append(',').Append(each.MaxIterations.ToString(CultureInfo.InvariantCulture)).Append("){");
                    AppendJoined(each.Body, AppendStatement);
                    _builder.Append('}');
                    break;
            }
        }

        private void AppendExpression(ExpressionSyntax expression)
        {
            switch (expression)
            {
                case LiteralExpressionSyntax literal:
                    _builder.Append("K[").Append(literal.Type.Name).Append(':').Append(FormatLiteral(literal)).Append(']');
                    break;
                case SomeExpressionSyntax some:
                    _builder.Append("P(");
                    AppendExpression(some.Value);
                    _builder.Append(')');
                    break;
                case NoneExpressionSyntax none:
                    _builder.Append("N[").Append(none.ElementType.Name).Append(']');
                    break;
                case OkExpressionSyntax ok:
                    _builder.Append("Y(");
                    AppendExpression(ok.Value);
                    _builder.Append(')');
                    break;
                case ErrExpressionSyntax err:
                    _builder.Append("Z[").Append(err.ValueType.Name).Append("](");
                    AppendExpression(err.Error);
                    _builder.Append(')');
                    break;
                case ListExpressionSyntax list:
                    _builder.Append("Q[").Append(list.ElementType.Name).Append("](");
                    AppendJoined(list.Elements, AppendExpression);
                    _builder.Append(')');
                    break;
                case RecordExpressionSyntax record:
                    _builder.Append("W[").Append(record.TypeName).Append("](");
                    AppendJoined(OrderInitializers(record), initializer =>
                    {
                        _builder.Append(initializer.Name).Append('=');
                        AppendExpression(initializer.Value);
                    });
                    _builder.Append(')');
                    break;
                case FieldExpressionSyntax field:
                    _builder.Append("G[").Append(field.FieldName).Append("](");
                    AppendExpression(field.Target);
                    _builder.Append(')');
                    break;
                case ReferenceExpressionSyntax reference:
                    _builder.Append("V[").Append(reference.Name).Append(']');
                    break;
                case CallExpressionSyntax call:
                    _builder.Append("A[").Append(call.FunctionName).Append('(');
                    AppendJoined(call.Arguments, AppendExpression);
                    _builder.Append(")]");
                    break;
            }
        }

        private IEnumerable<RecordInitializerSyntax> OrderInitializers(RecordExpressionSyntax record)
        {
            if (!_records.TryGetValue(record.TypeName, out var declaration))
            {
                return record.Fields;
            }

            return declaration.Fields
                .Select(field => record.Fields.FirstOrDefault(initializer =>
                    string.Equals(initializer.Name, field.Name, StringComparison.Ordinal)))
                .Where(static initializer => initializer is not null)
                .Select(static initializer => initializer!);
        }

        private static string FormatLiteral(LiteralExpressionSyntax literal) => literal.Type.Name switch
        {
            "null" => "null",
            "bool" => (bool)literal.Value! ? "true" : "false",
            "str" => JsonSerializer.Serialize((string)literal.Value!),
            "f64" => ((double)literal.Value!).ToString("R", CultureInfo.InvariantCulture),
            _ => Convert.ToString(literal.Value, CultureInfo.InvariantCulture) ?? "null"
        };

        private void AppendJoined<T>(IEnumerable<T> values, Action<T> append)
        {
            var first = true;
            foreach (var value in values)
            {
                if (!first)
                {
                    _builder.Append(';');
                }

                append(value);
                first = false;
            }
        }
    }
}
