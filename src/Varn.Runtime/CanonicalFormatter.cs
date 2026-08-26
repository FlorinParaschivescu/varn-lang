using System.Globalization;
using System.Text;
using System.Text.Json;
using Varn.Syntax;

namespace Varn.Runtime;

public static class CanonicalFormatter
{
    public static string Format(ProgramSyntax program)
    {
        var builder = new StringBuilder("P{");
        builder.Append("C[");
        AppendJoined(builder, program.Capabilities.Order(StringComparer.Ordinal), static (target, capability) => target.Append(capability));
        builder.Append("];B[").Append(program.StepBudget?.ToString(CultureInfo.InvariantCulture) ?? "?").Append("];F[");
        AppendJoined(builder, program.Functions, AppendFunction);
        return builder.Append("]}").ToString();
    }

    private static void AppendFunction(StringBuilder builder, FunctionSyntax function)
    {
        builder.Append(function.Name).Append('(');
        AppendJoined(builder, function.Parameters, static (target, parameter) =>
            target.Append(parameter.Name).Append(':').Append(parameter.Type.Name));
        builder.Append(")->").Append(function.ReturnType.Name).Append("![");
        AppendJoined(builder, function.Effects.Order(StringComparer.Ordinal), static (target, effect) => target.Append(effect));
        builder.Append("]{");
        AppendJoined(builder, function.Body, AppendStatement);
        builder.Append('}');
    }

    private static void AppendStatement(StringBuilder builder, StatementSyntax statement)
    {
        switch (statement)
        {
            case LetStatementSyntax let:
                builder.Append("L(").Append(let.Name).Append(':').Append(let.Type.Name).Append(',');
                AppendExpression(builder, let.Value);
                builder.Append(')');
                break;
            case VarStatementSyntax variable:
                builder.Append("M(").Append(variable.Name).Append(':').Append(variable.Type.Name).Append(',');
                AppendExpression(builder, variable.Value);
                builder.Append(')');
                break;
            case SetStatementSyntax assignment:
                builder.Append("S(").Append(assignment.Name).Append(',');
                AppendExpression(builder, assignment.Value);
                builder.Append(')');
                break;
            case ExpressionStatementSyntax expressionStatement:
                builder.Append("E(");
                AppendExpression(builder, expressionStatement.Expression);
                builder.Append(')');
                break;
            case ReturnStatementSyntax returnStatement:
                builder.Append("R(");
                AppendExpression(builder, returnStatement.Value);
                builder.Append(')');
                break;
            case IfStatementSyntax conditional:
                builder.Append("I(");
                AppendExpression(builder, conditional.Condition);
                builder.Append("){T{");
                AppendJoined(builder, conditional.ThenBody, AppendStatement);
                builder.Append("};E{");
                AppendJoined(builder, conditional.ElseBody, AppendStatement);
                builder.Append("}}");
                break;
            case LoopStatementSyntax loop:
                builder.Append("O(")
                    .Append(loop.Iterator).Append(':').Append(loop.IteratorType.Name).Append(',')
                    .Append(loop.StartInclusive.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(loop.EndExclusive.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(loop.MaxIterations.ToString(CultureInfo.InvariantCulture)).Append("){");
                AppendJoined(builder, loop.Body, AppendStatement);
                builder.Append('}');
                break;
        }
    }

    private static void AppendExpression(StringBuilder builder, ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal:
                builder.Append("K[").Append(literal.Type.Name).Append(':').Append(FormatLiteral(literal)).Append(']');
                break;
            case ReferenceExpressionSyntax reference:
                builder.Append("V[").Append(reference.Name).Append(']');
                break;
            case CallExpressionSyntax call:
                builder.Append("A[").Append(call.FunctionName).Append('(');
                AppendJoined(builder, call.Arguments, AppendExpression);
                builder.Append(")]");
                break;
        }
    }

    private static string FormatLiteral(LiteralExpressionSyntax literal) => literal.Type.Name switch
    {
        "null" => "null",
        "bool" => (bool)literal.Value! ? "true" : "false",
        "str" => JsonSerializer.Serialize((string)literal.Value!),
        "f64" => ((double)literal.Value!).ToString("R", CultureInfo.InvariantCulture),
        _ => Convert.ToString(literal.Value, CultureInfo.InvariantCulture) ?? "null"
    };

    private static void AppendJoined<T>(StringBuilder builder, IEnumerable<T> values, Action<StringBuilder, T> append)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(';');
            }

            append(builder, value);
            first = false;
        }
    }
}
