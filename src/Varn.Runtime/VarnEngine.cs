using Varn.ModuleSdk;
using Varn.Parser;
using Varn.Syntax;
using Varn.TypeSystem;

namespace Varn.Runtime;

public sealed class VarnEngine
{
    private readonly VarnModuleRegistry _modules = new();

    public VarnEngine(IEnumerable<IVarnModule>? modules = null)
    {
        foreach (var module in modules ?? [])
        {
            _modules.Add(module);
        }
    }

    public VarnModuleRegistry Modules => _modules;

    public void AddModule(IVarnModule module) => _modules.Add(module);

    public VarnCheckResult Check(string source)
    {
        var parsed = VarnParser.Parse(source);
        if (parsed.Diagnostics.Count > 0)
        {
            return new VarnCheckResult(parsed.Program, parsed.Diagnostics);
        }

        var checkedProgram = new VarnTypeChecker(_modules).Check(parsed.Program);
        return new VarnCheckResult(parsed.Program, checkedProgram.Diagnostics);
    }

    public async ValueTask<VarnRunResult> RunAsync(
        string source,
        VarnRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new VarnRunOptions();
        var check = Check(source);
        if (!check.IsValid)
        {
            return new VarnRunResult(null, check.Diagnostics, 0);
        }

        var binding = VarnInputBinder.Bind(
            VarnProgramContract.InputShape(check.Program),
            VarnProgramContract.RecordShapes(check.Program),
            options.Input);
        if (!binding.IsValid)
        {
            return new VarnRunResult(null, binding.Diagnostics, 0);
        }

        var interpreter = new Interpreter(check.Program, _modules, options);
        try
        {
            var value = await interpreter.RunAsync(binding.Value, cancellationToken).ConfigureAwait(false);
            return new VarnRunResult(value, [], interpreter.Steps);
        }
        catch (VarnExecutionException exception)
        {
            return new VarnRunResult(
                null,
                [new Diagnostic(exception.Code, exception.Message, exception.Span)],
                interpreter.Steps);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new VarnRunResult(
                null,
                [new Diagnostic("VARN4004", "Execution was cancelled.", check.Program.Span)],
                interpreter.Steps);
        }
    }

    private sealed class Interpreter
    {
        private readonly VarnModuleRegistry _modules;
        private readonly VarnRunOptions _options;
        private readonly IReadOnlyDictionary<string, FunctionSyntax> _functions;
        private readonly IReadOnlyDictionary<string, VarnRecordShape> _records;
        private readonly long _stepLimit;
        private SourceSpan _currentSpan;

        public Interpreter(ProgramSyntax program, VarnModuleRegistry modules, VarnRunOptions options)
        {
            _modules = modules;
            _options = options;
            _functions = program.Functions.ToDictionary(static function => function.Name, StringComparer.Ordinal);
            _records = VarnProgramContract.RecordShapes(program);
            _stepLimit = Math.Min(program.StepBudget!.Value, options.MaxSteps);
            _currentSpan = program.Span;
        }

        public long Steps { get; private set; }

        public ValueTask<VarnValue> RunAsync(VarnValue? input, CancellationToken cancellationToken) =>
            InvokeUserFunctionAsync(
                _functions[VarnProgramContract.EntryPointName],
                input is { } value ? [value] : [],
                cancellationToken);

        private async ValueTask<VarnValue> InvokeUserFunctionAsync(
            FunctionSyntax function,
            IReadOnlyList<VarnValue> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsumeStep(function.Span);
            var frame = new Dictionary<string, SlotCell>(StringComparer.Ordinal);
            for (var index = 0; index < function.Parameters.Count; index++)
            {
                frame.Add(function.Parameters[index].Name, new SlotCell(arguments[index]));
            }

            var result = await ExecuteStatementsAsync(function.Body, frame, cancellationToken).ConfigureAwait(false);
            if (result.HasReturn)
            {
                return result.ReturnValue;
            }

            throw new VarnExecutionException("VARN4000", $"Function '{function.Name}' completed without a return value.", function.Span);
        }

        private async ValueTask<StatementResult> ExecuteStatementsAsync(
            IReadOnlyList<StatementSyntax> statements,
            Dictionary<string, SlotCell> frame,
            CancellationToken cancellationToken)
        {
            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ConsumeStep(statement.Span);
                switch (statement)
                {
                    case LetStatementSyntax let:
                        frame.Add(
                            let.Name,
                            new SlotCell(await EvaluateAsync(let.Value, frame, cancellationToken).ConfigureAwait(false)));
                        break;
                    case VarStatementSyntax variable:
                        frame.Add(
                            variable.Name,
                            new SlotCell(await EvaluateAsync(variable.Value, frame, cancellationToken).ConfigureAwait(false)));
                        break;
                    case SetStatementSyntax assignment:
                        frame[assignment.Name].Value = await EvaluateAsync(assignment.Value, frame, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case ExpressionStatementSyntax expressionStatement:
                        _ = await EvaluateAsync(expressionStatement.Expression, frame, cancellationToken).ConfigureAwait(false);
                        break;
                    case ReturnStatementSyntax returnStatement:
                        return StatementResult.Return(
                            await EvaluateAsync(returnStatement.Value, frame, cancellationToken).ConfigureAwait(false));
                    case IfStatementSyntax conditional:
                        var condition = await EvaluateAsync(conditional.Condition, frame, cancellationToken).ConfigureAwait(false);
                        var selectedBody = condition.AsBool() ? conditional.ThenBody : conditional.ElseBody;
                        var branchResult = await ExecuteStatementsAsync(
                            selectedBody,
                            new Dictionary<string, SlotCell>(frame, StringComparer.Ordinal),
                            cancellationToken).ConfigureAwait(false);
                        if (branchResult.HasReturn)
                        {
                            return branchResult;
                        }

                        break;
                    case IfLetStatementSyntax ifLet:
                        var optional = await EvaluateAsync(ifLet.Optional, frame, cancellationToken).ConfigureAwait(false);
                        var optionalFrame = new Dictionary<string, SlotCell>(frame, StringComparer.Ordinal);
                        var optionalBody = ifLet.ElseBody;
                        if (optional.IsSome)
                        {
                            optionalFrame.Add(ifLet.Binding, new SlotCell(optional.AsOptionalValue()));
                            optionalBody = ifLet.ThenBody;
                        }

                        var optionalResult = await ExecuteStatementsAsync(optionalBody, optionalFrame, cancellationToken)
                            .ConfigureAwait(false);
                        if (optionalResult.HasReturn)
                        {
                            return optionalResult;
                        }

                        break;
                    case IfOkStatementSyntax ifOk:
                        var resultValue = await EvaluateAsync(ifOk.Result, frame, cancellationToken).ConfigureAwait(false);
                        var okFrame = new Dictionary<string, SlotCell>(frame, StringComparer.Ordinal);
                        var okBody = ifOk.ElseBody;
                        if (resultValue.IsOk)
                        {
                            okFrame.Add(ifOk.Binding, new SlotCell(resultValue.AsResult().Value));
                            okBody = ifOk.ThenBody;
                        }
                        else if (ifOk.ErrorBinding is { } errorBinding)
                        {
                            okFrame.Add(errorBinding, new SlotCell(resultValue.AsResult().Value));
                        }

                        var okResult = await ExecuteStatementsAsync(okBody, okFrame, cancellationToken)
                            .ConfigureAwait(false);
                        if (okResult.HasReturn)
                        {
                            return okResult;
                        }

                        break;
                    case LoopStatementSyntax loop:
                        for (var current = loop.StartInclusive; current < loop.EndExclusive; current++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            ConsumeStep(loop.Span);
                            var iterationFrame = new Dictionary<string, SlotCell>(frame, StringComparer.Ordinal)
                            {
                                [loop.Iterator] = new SlotCell(VarnValue.From(current))
                            };
                            var loopResult = await ExecuteStatementsAsync(loop.Body, iterationFrame, cancellationToken)
                                .ConfigureAwait(false);
                            if (loopResult.HasReturn)
                            {
                                return loopResult;
                            }
                        }

                        break;
                    case EachStatementSyntax each:
                        var values = (await EvaluateAsync(each.List, frame, cancellationToken).ConfigureAwait(false)).AsList();
                        if (values.Count > each.MaxIterations)
                        {
                            throw new VarnExecutionException(
                                "VARN4006",
                                $"List length {values.Count} exceeded each max {each.MaxIterations}.",
                                each.Span);
                        }

                        foreach (var value in values)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            ConsumeStep(each.Span);
                            var iterationFrame = new Dictionary<string, SlotCell>(frame, StringComparer.Ordinal)
                            {
                                [each.Iterator] = new SlotCell(value)
                            };
                            var eachResult = await ExecuteStatementsAsync(each.Body, iterationFrame, cancellationToken)
                                .ConfigureAwait(false);
                            if (eachResult.HasReturn)
                            {
                                return eachResult;
                            }
                        }

                        break;
                }
            }

            return StatementResult.Continue;
        }

        private async ValueTask<VarnValue> EvaluateAsync(
            ExpressionSyntax expression,
            IReadOnlyDictionary<string, SlotCell> frame,
            CancellationToken cancellationToken)
        {
            _currentSpan = expression.Span;
            return expression switch
            {
                LiteralExpressionSyntax literal => new VarnValue(literal.Type, literal.Value),
                SomeExpressionSyntax some => VarnValue.Some(
                    await EvaluateAsync(some.Value, frame, cancellationToken).ConfigureAwait(false)),
                NoneExpressionSyntax none => VarnValue.None(none.ElementType),
                OkExpressionSyntax ok => VarnValue.Ok(
                    await EvaluateAsync(ok.Value, frame, cancellationToken).ConfigureAwait(false)),
                ErrExpressionSyntax err => VarnValue.Err(
                    err.ValueType,
                    (string)(await EvaluateAsync(err.Error, frame, cancellationToken).ConfigureAwait(false)).Value!),
                ListExpressionSyntax list => await EvaluateListAsync(list, frame, cancellationToken).ConfigureAwait(false),
                RecordExpressionSyntax record => await EvaluateRecordAsync(record, frame, cancellationToken).ConfigureAwait(false),
                FieldExpressionSyntax field => await EvaluateFieldAsync(field, frame, cancellationToken).ConfigureAwait(false),
                ReferenceExpressionSyntax reference => frame[reference.Name].Value,
                LogicalExpressionSyntax logical => await EvaluateLogicalAsync(logical, frame, cancellationToken).ConfigureAwait(false),
                CallExpressionSyntax call => await InvokeCallAsync(call, frame, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown expression node {expression.GetType().Name}.")
            };
        }

        /// <summary>
        /// Evaluates the right operand only when the left does not already decide the answer. This
        /// makes the step count depend on the data, exactly as a loop over a host list already
        /// does; the run stays deterministic because the same input charges the same steps.
        /// </summary>
        private async ValueTask<VarnValue> EvaluateLogicalAsync(
            LogicalExpressionSyntax logical,
            IReadOnlyDictionary<string, SlotCell> frame,
            CancellationToken cancellationToken)
        {
            ConsumeStep(logical.Span);
            var left = await EvaluateAsync(logical.Left, frame, cancellationToken).ConfigureAwait(false);
            if (left.AsBool() != logical.IsAnd)
            {
                return left;
            }

            return await EvaluateAsync(logical.Right, frame, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask<VarnValue> EvaluateListAsync(
            ListExpressionSyntax list,
            IReadOnlyDictionary<string, SlotCell> frame,
            CancellationToken cancellationToken)
        {
            var values = new VarnValue[list.Elements.Count];
            for (var index = 0; index < list.Elements.Count; index++)
            {
                ConsumeStep(list.Elements[index].Span);
                values[index] = await EvaluateAsync(list.Elements[index], frame, cancellationToken).ConfigureAwait(false);
            }

            return VarnValue.FromList(list.ElementType, values);
        }

        private async ValueTask<VarnValue> EvaluateRecordAsync(
            RecordExpressionSyntax record,
            IReadOnlyDictionary<string, SlotCell> frame,
            CancellationToken cancellationToken)
        {
            var shape = _records[record.TypeName];
            var values = new VarnValue[shape.Fields.Count];
            for (var index = 0; index < shape.Fields.Count; index++)
            {
                var initializer = record.Fields.Single(field =>
                    string.Equals(field.Name, shape.Fields[index].Name, StringComparison.Ordinal));
                ConsumeStep(initializer.Value.Span);
                values[index] = await EvaluateAsync(initializer.Value, frame, cancellationToken).ConfigureAwait(false);
            }

            return VarnValue.FromRecord(shape, values);
        }

        private async ValueTask<VarnValue> EvaluateFieldAsync(
            FieldExpressionSyntax field,
            IReadOnlyDictionary<string, SlotCell> frame,
            CancellationToken cancellationToken)
        {
            ConsumeStep(field.Span);
            var target = await EvaluateAsync(field.Target, frame, cancellationToken).ConfigureAwait(false);
            return target.AsRecord().GetField(field.FieldName);
        }

        private async ValueTask<VarnValue> InvokeCallAsync(
            CallExpressionSyntax call,
            IReadOnlyDictionary<string, SlotCell> frame,
            CancellationToken cancellationToken)
        {
            ConsumeStep(call.Span);
            var arguments = new VarnValue[call.Arguments.Count];
            for (var index = 0; index < call.Arguments.Count; index++)
            {
                arguments[index] = await EvaluateAsync(call.Arguments[index], frame, cancellationToken).ConfigureAwait(false);
            }

            if (_functions.TryGetValue(call.FunctionName, out var function))
            {
                return await InvokeUserFunctionAsync(function, arguments, cancellationToken).ConfigureAwait(false);
            }

            if (call.FunctionName == "list.length")
            {
                return VarnValue.From((long)arguments[0].AsList().Count);
            }

            if (call.FunctionName == "list.append")
            {
                var values = arguments[0].AsList();
                if (values.Count >= VarnValue.MaxListElements)
                {
                    throw new VarnExecutionException(
                        "VARN4007",
                        $"Appending would exceed the {VarnValue.MaxListElements}-element list ceiling.",
                        call.Span);
                }

                return VarnValue.FromList(
                    arguments[0].Type.ListElementType!,
                    [.. values, arguments[1]]);
            }

            if (call.FunctionName == "list.get")
            {
                var values = arguments[0].AsList();
                var index = arguments[1].AsI64();
                return index >= 0 && index < values.Count
                    ? VarnValue.Some(values[(int)index])
                    : VarnValue.None(arguments[0].Type.ListElementType!);
            }

            var moduleFunction = _modules.Resolve(call.FunctionName, arguments.Select(static argument => argument.Type).ToArray())
                ?? throw new VarnExecutionException("VARN4001", $"Function '{call.FunctionName}' is unavailable at runtime.", call.Span);

            var requiredCapability = moduleFunction.Signature.Capability;
            if (requiredCapability is not null && !_options.AllowedCapabilities.Contains(requiredCapability))
            {
                throw new VarnExecutionException(
                    "VARN4002",
                    $"Host policy did not grant capability '{requiredCapability}' for call '{call.FunctionName}'.",
                    call.Span);
            }

            var context = new VarnCallContext(_options.Output, () => ConsumeStep(_currentSpan));
            try
            {
                return await moduleFunction.Handler(context, arguments, cancellationToken).ConfigureAwait(false);
            }
            catch (VarnExecutionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new VarnExecutionException(
                    "VARN4003",
                    $"Module '{moduleFunction.ModuleName}' failed in '{call.SourceName}': {exception.Message}",
                    call.Span);
            }
        }

        private void ConsumeStep(SourceSpan span)
        {
            _currentSpan = span;
            Steps++;
            if (Steps > _stepLimit)
            {
                throw new VarnExecutionException(
                    "VARN4005",
                    $"Execution exceeded the effective step budget of {_stepLimit}.",
                    span);
            }
        }

        private sealed class SlotCell(VarnValue value)
        {
            public VarnValue Value { get; set; } = value;
        }

        private readonly record struct StatementResult(bool HasReturn, VarnValue ReturnValue)
        {
            public static StatementResult Continue => new(false, VarnValue.Null);

            public static StatementResult Return(VarnValue value) => new(true, value);
        }
    }
}

public sealed class VarnExecutionException : Exception
{
    public VarnExecutionException(string code, string message, SourceSpan span)
        : base(message)
    {
        Code = code;
        Span = span;
    }

    public string Code { get; }

    public SourceSpan Span { get; }
}
