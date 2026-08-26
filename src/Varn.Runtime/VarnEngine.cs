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

        var interpreter = new Interpreter(check.Program, _modules, options);
        try
        {
            var value = await interpreter.RunAsync(cancellationToken).ConfigureAwait(false);
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
        private readonly long _stepLimit;
        private SourceSpan _currentSpan;

        public Interpreter(ProgramSyntax program, VarnModuleRegistry modules, VarnRunOptions options)
        {
            _modules = modules;
            _options = options;
            _functions = program.Functions.ToDictionary(static function => function.Name, StringComparer.Ordinal);
            _stepLimit = Math.Min(program.StepBudget!.Value, options.MaxSteps);
            _currentSpan = program.Span;
        }

        public long Steps { get; private set; }

        public ValueTask<VarnValue> RunAsync(CancellationToken cancellationToken) =>
            InvokeUserFunctionAsync(_functions["main"], [], cancellationToken);

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
                ReferenceExpressionSyntax reference => frame[reference.Name].Value,
                CallExpressionSyntax call => await InvokeCallAsync(call, frame, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown expression node {expression.GetType().Name}.")
            };
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
                    $"Module '{moduleFunction.ModuleName}' failed in '{call.FunctionName}': {exception.Message}",
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
