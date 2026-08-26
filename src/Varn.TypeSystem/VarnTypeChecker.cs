using System.Numerics;
using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.TypeSystem;

public sealed class VarnTypeChecker
{
    private static readonly HashSet<string> KnownTypes =
        ["i64", "f64", "bool", "str", "null"];

    private readonly VarnModuleRegistry _modules;
    private readonly List<Diagnostic> _diagnostics = [];
    private IReadOnlyDictionary<string, FunctionSyntax> _functions =
        new Dictionary<string, FunctionSyntax>();
    private ProgramSyntax _program = null!;

    public VarnTypeChecker(VarnModuleRegistry modules)
    {
        _modules = modules;
    }

    public TypeCheckResult Check(ProgramSyntax program)
    {
        _diagnostics.Clear();
        _program = program;

        if (program.StepBudget is null)
        {
            Report("VARN3000", "A program must declare budget[steps=...].", program.Span);
        }
        else if (program.StepBudget <= 0)
        {
            Report("VARN3001", "The step budget must be greater than zero.", program.Span);
        }

        ReportDuplicates(program.Capabilities, "capability", program.Span);

        var functionGroups = program.Functions.GroupBy(static function => function.Name, StringComparer.Ordinal);
        foreach (var group in functionGroups.Where(static group => group.Count() > 1))
        {
            Report("VARN3002", $"Function '{group.Key}' is declared more than once.", group.Last().Span);
        }

        _functions = functionGroups.ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        if (!_functions.TryGetValue("main", out var main))
        {
            Report("VARN3003", "A program must declare fn main()->i64.", program.Span);
        }
        else if (main.Parameters.Count != 0 || main.ReturnType != VarnType.I64)
        {
            Report("VARN3004", "The entry point must have the signature fn main()->i64.", main.Span);
        }

        foreach (var function in program.Functions)
        {
            CheckFunction(function);
        }

        return new TypeCheckResult(_diagnostics.ToArray());
    }

    private void CheckFunction(FunctionSyntax function)
    {
        CheckType(function.ReturnType, function.Span);
        ReportDuplicates(function.Effects, "effect", function.Span);

        var symbols = new Dictionary<string, SlotSymbol>(StringComparer.Ordinal);
        foreach (var parameter in function.Parameters)
        {
            CheckType(parameter.Type, parameter.Span);
            if (!symbols.TryAdd(parameter.Name, new SlotSymbol(parameter.Type, IsMutable: false)))
            {
                Report("VARN3005", $"Slot '{parameter.Name}' is declared more than once.", parameter.Span);
            }
        }

        CheckStatements(function.Body, function, symbols);
        if (function.Body.LastOrDefault() is not ReturnStatementSyntax)
        {
            Report("VARN3009", $"Function '{function.Name}' must end with 'ret'.", function.Span);
        }
    }

    private void CheckStatements(
        IReadOnlyList<StatementSyntax> statements,
        FunctionSyntax function,
        Dictionary<string, SlotSymbol> symbols)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case LetStatementSyntax let:
                    CheckType(let.Type, let.Span);
                    var valueType = CheckExpression(let.Value, function, symbols);
                    if (!IsAssignable(let.Type, valueType))
                    {
                        Report("VARN3006", $"Cannot assign {valueType} to slot '{let.Name}' of type {let.Type}.", let.Span);
                    }

                    if (!symbols.TryAdd(let.Name, new SlotSymbol(let.Type, IsMutable: false)))
                    {
                        Report("VARN3005", $"Slot '{let.Name}' is declared more than once.", let.Span);
                    }

                    break;
                case VarStatementSyntax variable:
                    CheckType(variable.Type, variable.Span);
                    var initialType = CheckExpression(variable.Value, function, symbols);
                    if (!IsAssignable(variable.Type, initialType))
                    {
                        Report("VARN3006", $"Cannot assign {initialType} to slot '{variable.Name}' of type {variable.Type}.", variable.Span);
                    }

                    if (!symbols.TryAdd(variable.Name, new SlotSymbol(variable.Type, IsMutable: true)))
                    {
                        Report("VARN3005", $"Slot '{variable.Name}' is declared more than once.", variable.Span);
                    }

                    break;
                case SetStatementSyntax assignment:
                    var assignedType = CheckExpression(assignment.Value, function, symbols);
                    if (!symbols.TryGetValue(assignment.Name, out var target))
                    {
                        Report("VARN3010", $"Slot '{assignment.Name}' is not defined.", assignment.Span);
                        break;
                    }

                    if (!target.IsMutable)
                    {
                        Report("VARN3024", $"Slot '{assignment.Name}' is immutable and cannot be assigned.", assignment.Span);
                    }

                    if (!IsAssignable(target.Type, assignedType))
                    {
                        Report("VARN3025", $"Cannot assign {assignedType} to mutable slot '{assignment.Name}' of type {target.Type}.", assignment.Span);
                    }

                    break;
                case ExpressionStatementSyntax expressionStatement:
                    var expressionType = CheckExpression(expressionStatement.Expression, function, symbols);
                    if (expressionType != VarnType.Null)
                    {
                        Report("VARN3007", "Only a null-returning call may be used as a statement.", expressionStatement.Span);
                    }

                    break;
                case ReturnStatementSyntax returnStatement:
                    var returnType = CheckExpression(returnStatement.Value, function, symbols);
                    if (!IsAssignable(function.ReturnType, returnType))
                    {
                        Report("VARN3008", $"Function '{function.Name}' must return {function.ReturnType}, not {returnType}.", returnStatement.Span);
                    }

                    break;
                case IfStatementSyntax conditional:
                    var conditionType = CheckExpression(conditional.Condition, function, symbols);
                    if (conditionType != VarnType.Bool)
                    {
                        Report("VARN3019", $"An if condition must be bool, not {conditionType}.", conditional.Condition.Span);
                    }

                    CheckStatements(
                        conditional.ThenBody,
                        function,
                        new Dictionary<string, SlotSymbol>(symbols, StringComparer.Ordinal));
                    CheckStatements(
                        conditional.ElseBody,
                        function,
                        new Dictionary<string, SlotSymbol>(symbols, StringComparer.Ordinal));
                    break;
                case LoopStatementSyntax loop:
                    CheckLoop(loop, function, symbols);
                    break;
            }
        }
    }

    private void CheckLoop(
        LoopStatementSyntax loop,
        FunctionSyntax function,
        IReadOnlyDictionary<string, SlotSymbol> symbols)
    {
        if (loop.IteratorType != VarnType.I64)
        {
            Report("VARN3020", $"Loop iterator '{loop.Iterator}' must have type i64.", loop.Span);
        }

        if (loop.MaxIterations < 0)
        {
            Report("VARN3021", "A loop max must be nonnegative.", loop.Span);
        }

        if (loop.EndExclusive < loop.StartInclusive)
        {
            Report("VARN3022", "A loop end must be greater than or equal to its start.", loop.Span);
        }
        else
        {
            var requiredIterations = new BigInteger(loop.EndExclusive) - new BigInteger(loop.StartInclusive);
            if (requiredIterations != loop.MaxIterations)
            {
                Report(
                    "VARN3023",
                    $"Loop max {loop.MaxIterations} must equal its statically known iteration count {requiredIterations}.",
                    loop.Span);
            }
        }

        var loopSymbols = new Dictionary<string, SlotSymbol>(symbols, StringComparer.Ordinal);
        if (!loopSymbols.TryAdd(loop.Iterator, new SlotSymbol(loop.IteratorType, IsMutable: false)))
        {
            Report("VARN3005", $"Slot '{loop.Iterator}' is declared more than once.", loop.Span);
        }

        CheckStatements(loop.Body, function, loopSymbols);
    }

    private VarnType CheckExpression(
        ExpressionSyntax expression,
        FunctionSyntax containingFunction,
        IReadOnlyDictionary<string, SlotSymbol> symbols)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal:
                return literal.Type;
            case ReferenceExpressionSyntax reference:
                if (symbols.TryGetValue(reference.Name, out var symbolType))
                {
                    return symbolType.Type;
                }

                Report("VARN3010", $"Slot '{reference.Name}' is not defined.", reference.Span);
                return VarnType.Null;
            case CallExpressionSyntax call:
                return CheckCall(call, containingFunction, symbols);
            default:
                throw new InvalidOperationException($"Unknown expression node {expression.GetType().Name}.");
        }
    }

    private VarnType CheckCall(
        CallExpressionSyntax call,
        FunctionSyntax containingFunction,
        IReadOnlyDictionary<string, SlotSymbol> symbols)
    {
        var argumentTypes = call.Arguments
            .Select(argument => CheckExpression(argument, containingFunction, symbols))
            .ToArray();

        if (_functions.TryGetValue(call.FunctionName, out var target))
        {
            ValidateArguments(call, argumentTypes, target.Parameters.Select(static parameter => parameter.Type).ToArray());
            foreach (var effect in target.Effects)
            {
                RequireEffect(containingFunction, effect, call.Span, call.FunctionName);
            }

            return target.ReturnType;
        }

        var candidates = _modules.Find(call.FunctionName);
        var moduleFunction = _modules.Resolve(call.FunctionName, argumentTypes);
        if (moduleFunction is null)
        {
            if (candidates.Count == 0)
            {
                Report("VARN3011", $"Function '{call.FunctionName}' is not defined by this program or a loaded module.", call.Span);
            }
            else
            {
                var actual = string.Join(",", argumentTypes.Select(static type => type.Name));
                Report("VARN3012", $"No overload '{call.FunctionName}({actual})' exists.", call.Span);
            }

            return VarnType.Null;
        }

        var signature = moduleFunction.Signature;
        if (signature.Effect is not null)
        {
            RequireEffect(containingFunction, signature.Effect, call.Span, call.FunctionName);
        }

        if (signature.Capability is not null && !_program.Capabilities.Contains(signature.Capability, StringComparer.Ordinal))
        {
            Report(
                "VARN3013",
                $"Call '{call.FunctionName}' requires program capability '{signature.Capability}'.",
                call.Span);
        }

        return signature.ReturnType;
    }

    private void ValidateArguments(
        CallExpressionSyntax call,
        IReadOnlyList<VarnType> actual,
        IReadOnlyList<VarnType> expected)
    {
        if (actual.Count != expected.Count)
        {
            Report("VARN3014", $"Call '{call.FunctionName}' expects {expected.Count} arguments, got {actual.Count}.", call.Span);
            return;
        }

        for (var index = 0; index < actual.Count; index++)
        {
            if (!IsAssignable(expected[index], actual[index]))
            {
                Report("VARN3015", $"Argument {index} of '{call.FunctionName}' expects {expected[index]}, got {actual[index]}.", call.Arguments[index].Span);
            }
        }
    }

    private void RequireEffect(FunctionSyntax function, string effect, SourceSpan span, string calledFunction)
    {
        if (!function.Effects.Contains(effect, StringComparer.Ordinal))
        {
            Report("VARN3016", $"Call '{calledFunction}' requires effect '{effect}' on function '{function.Name}'.", span);
        }
    }

    private void CheckType(VarnType type, SourceSpan span)
    {
        if (!KnownTypes.Contains(type.Name))
        {
            Report("VARN3017", $"Unknown type '{type.Name}'.", span);
        }
    }

    private void ReportDuplicates(IEnumerable<string> values, string kind, SourceSpan span)
    {
        foreach (var duplicate in values.GroupBy(static value => value, StringComparer.Ordinal).Where(static group => group.Count() > 1))
        {
            Report("VARN3018", $"The {kind} '{duplicate.Key}' is declared more than once.", span);
        }
    }

    private static bool IsAssignable(VarnType expected, VarnType actual) =>
        expected == VarnType.Any || expected == actual;

    private void Report(string code, string message, SourceSpan span) =>
        _diagnostics.Add(new Diagnostic(code, message, span));

    private readonly record struct SlotSymbol(VarnType Type, bool IsMutable);
}
