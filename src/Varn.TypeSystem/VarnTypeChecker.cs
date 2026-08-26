using System.Numerics;
using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.TypeSystem;

public sealed class VarnTypeChecker
{
    private static readonly HashSet<string> KnownTypes =
        ["i64", "f64", "bool", "str", "null"];

    private static readonly HashSet<string> ReservedTypeNames =
        [.. KnownTypes, "any"];

    private readonly VarnModuleRegistry _modules;
    private readonly List<Diagnostic> _diagnostics = [];
    private IReadOnlyDictionary<string, FunctionSyntax> _functions =
        new Dictionary<string, FunctionSyntax>();
    private IReadOnlyDictionary<string, VarnRecordShape> _records =
        new Dictionary<string, VarnRecordShape>();
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
        _records = CollectRecords(program);

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

    private IReadOnlyDictionary<string, VarnRecordShape> CollectRecords(ProgramSyntax program)
    {
        var records = new Dictionary<string, VarnRecordShape>(StringComparer.Ordinal);
        foreach (var declaration in program.Records)
        {
            var fields = new List<VarnRecordField>();
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in declaration.Fields)
            {
                if (!declared.Add(field.Name))
                {
                    Report(
                        "VARN3037",
                        $"Record '{declaration.Name}' declares field '{field.Name}' more than once.",
                        field.Span);
                    continue;
                }

                if (!VarnValue.IsSupportedFieldType(field.Type))
                {
                    Report(
                        "VARN3038",
                        $"Record field type '{field.Type}' is not supported.",
                        field.Span);
                }

                fields.Add(new VarnRecordField(field.Name, field.Type));
            }

            if (ReservedTypeNames.Contains(declaration.Name))
            {
                Report(
                    "VARN3036",
                    $"Record '{declaration.Name}' shadows a built-in type name.",
                    declaration.Span);
                continue;
            }

            if (!records.TryAdd(declaration.Name, new VarnRecordShape(declaration.Name, fields)))
            {
                Report(
                    "VARN3036",
                    $"Record '{declaration.Name}' is declared more than once.",
                    declaration.Span);
            }
        }

        return records;
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
                case IfLetStatementSyntax ifLet:
                    CheckIfLet(ifLet, function, symbols);
                    break;
                case LoopStatementSyntax loop:
                    CheckLoop(loop, function, symbols);
                    break;
                case EachStatementSyntax each:
                    CheckEach(each, function, symbols);
                    break;
            }
        }
    }

    private void CheckIfLet(
        IfLetStatementSyntax ifLet,
        FunctionSyntax function,
        IReadOnlyDictionary<string, SlotSymbol> symbols)
    {
        CheckType(ifLet.BindingType, ifLet.Span);
        var optionalType = CheckExpression(ifLet.Optional, function, symbols);
        if (!optionalType.IsOptional)
        {
            Report("VARN3026", $"An if let source must be optional, not {optionalType}.", ifLet.Optional.Span);
        }
        else if (optionalType.OptionalElementType != ifLet.BindingType)
        {
            Report(
                "VARN3027",
                $"An if let binding of type {ifLet.BindingType} cannot extract {optionalType.OptionalElementType} from {optionalType}.",
                ifLet.Span);
        }

        var thenSymbols = new Dictionary<string, SlotSymbol>(symbols, StringComparer.Ordinal);
        if (!thenSymbols.TryAdd(ifLet.Binding, new SlotSymbol(ifLet.BindingType, IsMutable: false)))
        {
            Report("VARN3005", $"Slot '{ifLet.Binding}' is declared more than once.", ifLet.Span);
        }

        CheckStatements(ifLet.ThenBody, function, thenSymbols);
        CheckStatements(
            ifLet.ElseBody,
            function,
            new Dictionary<string, SlotSymbol>(symbols, StringComparer.Ordinal));
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

    private void CheckEach(
        EachStatementSyntax each,
        FunctionSyntax function,
        IReadOnlyDictionary<string, SlotSymbol> symbols)
    {
        CheckType(each.IteratorType, each.Span);
        var listType = CheckExpression(each.List, function, symbols);
        if (!listType.IsList)
        {
            Report("VARN3032", $"An each source must be a list, not {listType}.", each.List.Span);
        }
        else if (listType.ListElementType != each.IteratorType)
        {
            Report(
                "VARN3033",
                $"An each binding of type {each.IteratorType} cannot traverse elements of type {listType.ListElementType}.",
                each.Span);
        }

        if (each.MaxIterations < 0 || each.MaxIterations > VarnValue.MaxListElements)
        {
            Report(
                "VARN3034",
                $"An each max must be between 0 and {VarnValue.MaxListElements}.",
                each.Span);
        }

        var eachSymbols = new Dictionary<string, SlotSymbol>(symbols, StringComparer.Ordinal);
        if (!eachSymbols.TryAdd(each.Iterator, new SlotSymbol(each.IteratorType, IsMutable: false)))
        {
            Report("VARN3005", $"Slot '{each.Iterator}' is declared more than once.", each.Span);
        }

        CheckStatements(each.Body, function, eachSymbols);
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
            case SomeExpressionSyntax some:
                var valueType = CheckExpression(some.Value, containingFunction, symbols);
                var someType = VarnType.Optional(valueType);
                CheckType(someType, some.Span);
                return someType;
            case NoneExpressionSyntax none:
                var noneType = VarnType.Optional(none.ElementType);
                CheckType(noneType, none.Span);
                return noneType;
            case ListExpressionSyntax list:
                CheckType(VarnType.List(list.ElementType), list.Span);
                if (list.Elements.Count > VarnValue.MaxListElements)
                {
                    Report(
                        "VARN3031",
                        $"A list literal cannot contain more than {VarnValue.MaxListElements} elements.",
                        list.Span);
                }

                foreach (var element in list.Elements)
                {
                    var elementType = CheckExpression(element, containingFunction, symbols);
                    if (elementType != list.ElementType)
                    {
                        Report(
                            "VARN3030",
                            $"A list[{list.ElementType}] element cannot have type {elementType}.",
                            element.Span);
                    }
                }

                return VarnType.List(list.ElementType);
            case RecordExpressionSyntax record:
                return CheckRecord(record, containingFunction, symbols);
            case FieldExpressionSyntax field:
                var targetType = CheckExpression(field.Target, containingFunction, symbols);
                if (!_records.TryGetValue(targetType.Name, out var targetShape))
                {
                    Report("VARN3043", $"Field access requires a record value, not {targetType}.", field.Span);
                    return VarnType.Null;
                }

                var fieldIndex = targetShape.IndexOf(field.FieldName);
                if (fieldIndex < 0)
                {
                    Report(
                        "VARN3044",
                        $"Record '{targetShape.Name}' does not declare field '{field.FieldName}'.",
                        field.Span);
                    return VarnType.Null;
                }

                return targetShape.Fields[fieldIndex].Type;
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

    private VarnType CheckRecord(
        RecordExpressionSyntax record,
        FunctionSyntax containingFunction,
        IReadOnlyDictionary<string, SlotSymbol> symbols)
    {
        if (!_records.TryGetValue(record.TypeName, out var shape))
        {
            foreach (var initializer in record.Fields)
            {
                _ = CheckExpression(initializer.Value, containingFunction, symbols);
            }

            Report("VARN3017", $"Unknown type '{record.TypeName}'.", record.Span);
            return VarnType.Null;
        }

        var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var initializer in record.Fields)
        {
            var valueType = CheckExpression(initializer.Value, containingFunction, symbols);
            var index = shape.IndexOf(initializer.Name);
            if (index < 0)
            {
                Report(
                    "VARN3040",
                    $"Record '{shape.Name}' does not declare field '{initializer.Name}'.",
                    initializer.Span);
                continue;
            }

            if (!assigned.Add(initializer.Name))
            {
                Report(
                    "VARN3041",
                    $"Record '{shape.Name}' field '{initializer.Name}' is set more than once.",
                    initializer.Span);
            }

            if (!IsAssignable(shape.Fields[index].Type, valueType))
            {
                Report(
                    "VARN3042",
                    $"Field '{shape.Name}.{initializer.Name}' requires {shape.Fields[index].Type}, got {valueType}.",
                    initializer.Value.Span);
            }
        }

        var missing = shape.Fields
            .Where(field => !assigned.Contains(field.Name))
            .Select(static field => field.Name)
            .ToArray();
        if (missing.Length > 0)
        {
            Report(
                "VARN3039",
                $"Record '{shape.Name}' construction is missing field(s) {string.Join(", ", missing)}.",
                record.Span);
        }

        return shape.Type;
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

        if (call.FunctionName is "list.length" or "list.get")
        {
            return CheckListCall(call, argumentTypes);
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

    private VarnType CheckListCall(CallExpressionSyntax call, IReadOnlyList<VarnType> argumentTypes)
    {
        var expectedCount = call.FunctionName == "list.length" ? 1 : 2;
        if (argumentTypes.Count != expectedCount)
        {
            Report("VARN3014", $"Call '{call.FunctionName}' expects {expectedCount} arguments, got {argumentTypes.Count}.", call.Span);
            return VarnType.Null;
        }

        if (!argumentTypes[0].IsList)
        {
            Report("VARN3035", $"Argument 0 of '{call.FunctionName}' must be a list, got {argumentTypes[0]}.", call.Arguments[0].Span);
            return VarnType.Null;
        }

        if (call.FunctionName == "list.length")
        {
            return VarnType.I64;
        }

        if (argumentTypes[1] != VarnType.I64)
        {
            Report("VARN3015", $"Argument 1 of 'list.get' expects i64, got {argumentTypes[1]}.", call.Arguments[1].Span);
        }

        return VarnType.Optional(argumentTypes[0].ListElementType!);
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
        if (type.IsList)
        {
            var elementType = type.ListElementType!;
            if (elementType != VarnType.I64 && elementType != VarnType.F64 &&
                elementType != VarnType.Bool && elementType != VarnType.String)
            {
                Report("VARN3029", $"List element type '{elementType}' is not supported.", span);
            }

            return;
        }

        if (type.IsOptional)
        {
            var elementType = type.OptionalElementType!;
            if (elementType.IsOptional || elementType is null || elementType == VarnType.Null ||
                elementType == VarnType.Any || !KnownTypes.Contains(elementType.Name))
            {
                Report("VARN3028", $"Optional element type '{elementType}' is not supported.", span);
            }

            return;
        }

        if (!KnownTypes.Contains(type.Name) && !_records.ContainsKey(type.Name))
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
