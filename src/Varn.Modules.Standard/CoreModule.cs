using System.Globalization;
using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.Modules.Standard;

/// <summary>
/// The pure standard operations every Varn program may call. Each is total, deterministic,
/// capability-free, and exactly typed: there are no implicit conversions and no operation here
/// can fail at runtime. Failable operations wait for <c>Result</c>.
/// </summary>
public sealed class CoreModule : IVarnModule
{
    private static readonly VarnType[] OrderedTypes = [VarnType.I64, VarnType.F64, VarnType.String];
    private static readonly VarnType[] EquatableTypes = [VarnType.I64, VarnType.F64, VarnType.Bool, VarnType.String];

    public string Name => "varn.core";

    public void Register(VarnModuleBuilder builder)
    {
        RegisterArithmetic(builder);
        RegisterLogic(builder);
        RegisterComparisons(builder);
        RegisterStrings(builder);
        RegisterLists(builder);
        RegisterFailable(builder);
    }

    /// <summary>
    /// The operations that can fail in-domain. Each returns <c>result[T]</c> so the failure is a
    /// value the caller must inspect, never a diagnostic that aborts the run. Total <c>div</c> and
    /// <c>mod</c> still exist and still trap on a zero divisor, because a zero literal divisor is a
    /// defect rather than an expected outcome; reach for these when the divisor is data.
    /// </summary>
    private static void RegisterFailable(VarnModuleBuilder builder)
    {
        RegisterCheckedI64(builder, "num.div", static (left, right) => left / right);
        RegisterCheckedI64(builder, "num.mod", static (left, right) => left % right);

        builder.Function(
            new VarnFunctionSignature("num.to_f64", [VarnType.I64], VarnType.F64),
            static (_, arguments, _) => ValueTask.FromResult(VarnValue.From((double)arguments[0].AsI64())));

        builder.Function(
            new VarnFunctionSignature("num.to_i64", [VarnType.F64], VarnType.Result(VarnType.I64)),
            static (_, arguments, _) =>
            {
                var value = arguments[0].AsF64();
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    return ValueTask.FromResult(VarnValue.Err(VarnType.I64, "not a finite number"));
                }

                if (value != Math.Truncate(value))
                {
                    return ValueTask.FromResult(VarnValue.Err(VarnType.I64, "not a whole number"));
                }

                return ValueTask.FromResult(value is >= -9223372036854775808.0 and < 9223372036854775808.0
                    ? VarnValue.Ok(VarnValue.From((long)value))
                    : VarnValue.Err(VarnType.I64, "outside the i64 range"));
            });

        builder.Function(
            new VarnFunctionSignature("str.to_i64", [VarnType.String], VarnType.Result(VarnType.I64)),
            static (_, arguments, _) => ValueTask.FromResult(
                long.TryParse(Text(arguments[0]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? VarnValue.Ok(VarnValue.From(parsed))
                    : VarnValue.Err(VarnType.I64, "not an i64")));

        builder.Function(
            new VarnFunctionSignature("str.to_f64", [VarnType.String], VarnType.Result(VarnType.F64)),
            static (_, arguments, _) => ValueTask.FromResult(
                double.TryParse(Text(arguments[0]), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    && !double.IsNaN(parsed) && !double.IsInfinity(parsed)
                    ? VarnValue.Ok(VarnValue.From(parsed))
                    : VarnValue.Err(VarnType.F64, "not an f64")));
    }

    private static void RegisterCheckedI64(VarnModuleBuilder builder, string name, Func<long, long, long> operation)
    {
        builder.Function(
            new VarnFunctionSignature(name, [VarnType.I64, VarnType.I64], VarnType.Result(VarnType.I64)),
            (_, arguments, _) =>
            {
                var divisor = arguments[1].AsI64();
                if (divisor == 0)
                {
                    return ValueTask.FromResult(VarnValue.Err(VarnType.I64, "divide by zero"));
                }

                if (arguments[0].AsI64() == long.MinValue && divisor == -1)
                {
                    return ValueTask.FromResult(VarnValue.Err(VarnType.I64, "outside the i64 range"));
                }

                return ValueTask.FromResult(VarnValue.Ok(VarnValue.From(operation(arguments[0].AsI64(), divisor))));
            });
    }

    private static void RegisterArithmetic(VarnModuleBuilder builder)
    {
        RegisterI64(builder, "add", static (left, right) => checked(left + right));
        RegisterI64(builder, "sub", static (left, right) => checked(left - right));
        RegisterI64(builder, "mul", static (left, right) => checked(left * right));
        RegisterI64(builder, "div", static (left, right) => left / right);
        RegisterI64(builder, "mod", static (left, right) => left % right);
        RegisterI64(builder, "min", Math.Min);
        RegisterI64(builder, "max", Math.Max);

        RegisterF64(builder, "add", static (left, right) => left + right);
        RegisterF64(builder, "sub", static (left, right) => left - right);
        RegisterF64(builder, "mul", static (left, right) => left * right);
        RegisterF64(builder, "div", static (left, right) => left / right);
        RegisterF64(builder, "min", Math.Min);
        RegisterF64(builder, "max", Math.Max);

        builder.Function(
            new VarnFunctionSignature("abs", [VarnType.I64], VarnType.I64),
            static (_, arguments, _) => ValueTask.FromResult(VarnValue.From(Math.Abs(arguments[0].AsI64()))));
        builder.Function(
            new VarnFunctionSignature("abs", [VarnType.F64], VarnType.F64),
            static (_, arguments, _) => ValueTask.FromResult(VarnValue.From(Math.Abs(arguments[0].AsF64()))));
    }

    /// <summary>
    /// Boolean operations are ordinary calls, so both arguments are always evaluated. There is no
    /// short-circuiting: every call charges the same steps regardless of operand values, which keeps
    /// step accounting a function of program shape rather than data.
    /// </summary>
    private static void RegisterLogic(VarnModuleBuilder builder)
    {
        builder.Function(
            new VarnFunctionSignature("and", [VarnType.Bool, VarnType.Bool], VarnType.Bool),
            static (_, arguments, _) => ValueTask.FromResult(
                VarnValue.From(arguments[0].AsBool() && arguments[1].AsBool())));
        builder.Function(
            new VarnFunctionSignature("or", [VarnType.Bool, VarnType.Bool], VarnType.Bool),
            static (_, arguments, _) => ValueTask.FromResult(
                VarnValue.From(arguments[0].AsBool() || arguments[1].AsBool())));
        builder.Function(
            new VarnFunctionSignature("not", [VarnType.Bool], VarnType.Bool),
            static (_, arguments, _) => ValueTask.FromResult(VarnValue.From(!arguments[0].AsBool())));
    }

    /// <summary>
    /// <c>i64</c> and <c>str</c> compare by total order, strings ordinally. <c>f64</c> uses the IEEE
    /// 754 operators directly rather than <c>CompareTo</c>, so every ordering and equality test
    /// involving NaN is false and <c>ne</c> is true, matching the arithmetic Varn already exposes.
    /// </summary>
    private static void RegisterComparisons(VarnModuleBuilder builder)
    {
        foreach (var type in EquatableTypes)
        {
            if (type == VarnType.F64)
            {
                continue;
            }

            RegisterPredicate(builder, "eq", type, arguments => AreEqual(type, arguments));
            RegisterPredicate(builder, "ne", type, arguments => !AreEqual(type, arguments));
        }

        foreach (var type in OrderedTypes)
        {
            if (type == VarnType.F64)
            {
                continue;
            }

            RegisterPredicate(builder, "lt", type, arguments => Compare(type, arguments) < 0);
            RegisterPredicate(builder, "gt", type, arguments => Compare(type, arguments) > 0);
            RegisterPredicate(builder, "lte", type, arguments => Compare(type, arguments) <= 0);
            RegisterPredicate(builder, "gte", type, arguments => Compare(type, arguments) >= 0);
        }

        RegisterF64Predicate(builder, "eq", static (left, right) => left == right);
        RegisterF64Predicate(builder, "ne", static (left, right) => left != right);
        RegisterF64Predicate(builder, "lt", static (left, right) => left < right);
        RegisterF64Predicate(builder, "gt", static (left, right) => left > right);
        RegisterF64Predicate(builder, "lte", static (left, right) => left <= right);
        RegisterF64Predicate(builder, "gte", static (left, right) => left >= right);
    }

    private static void RegisterF64Predicate(
        VarnModuleBuilder builder,
        string name,
        Func<double, double, bool> operation)
    {
        builder.Function(
            new VarnFunctionSignature(name, [VarnType.F64, VarnType.F64], VarnType.Bool),
            (_, arguments, _) => ValueTask.FromResult(
                VarnValue.From(operation(arguments[0].AsF64(), arguments[1].AsF64()))));
    }

    private static void RegisterStrings(VarnModuleBuilder builder)
    {
        builder.Function(
            new VarnFunctionSignature("str.length", [VarnType.String], VarnType.I64),
            static (_, arguments, _) => ValueTask.FromResult(VarnValue.From((long)Text(arguments[0]).Length)));
        builder.Function(
            new VarnFunctionSignature("str.concat", [VarnType.String, VarnType.String], VarnType.String),
            static (_, arguments, _) => ValueTask.FromResult(
                VarnValue.From(string.Concat(Text(arguments[0]), Text(arguments[1])))));
        RegisterStringPredicate(builder, "str.contains", static (value, other) =>
            value.Contains(other, StringComparison.Ordinal));
        RegisterStringPredicate(builder, "str.starts_with", static (value, other) =>
            value.StartsWith(other, StringComparison.Ordinal));
        RegisterStringPredicate(builder, "str.ends_with", static (value, other) =>
            value.EndsWith(other, StringComparison.Ordinal));
    }

    private static void RegisterLists(VarnModuleBuilder builder)
    {
        foreach (var elementType in EquatableTypes)
        {
            var type = elementType;
            builder.Function(
                new VarnFunctionSignature("list.contains", [VarnType.List(type), type], VarnType.Bool),
                (context, arguments, _) =>
                {
                    var found = false;
                    foreach (var element in arguments[0].AsList())
                    {
                        context.ConsumeStep();
                        found = found || (type == VarnType.F64
                            ? element.AsF64() == arguments[1].AsF64()
                            : AreEqual(type, [element, arguments[1]]));
                    }

                    return ValueTask.FromResult(VarnValue.From(found));
                });
        }
    }

    private static bool AreEqual(VarnType type, IReadOnlyList<VarnValue> arguments) => type.Name switch
    {
        "i64" => arguments[0].AsI64() == arguments[1].AsI64(),
        "bool" => arguments[0].AsBool() == arguments[1].AsBool(),
        "str" => string.Equals(Text(arguments[0]), Text(arguments[1]), StringComparison.Ordinal),
        _ => throw new InvalidOperationException($"Type '{type}' has no total equality.")
    };

    private static int Compare(VarnType type, IReadOnlyList<VarnValue> arguments) => type.Name switch
    {
        "i64" => arguments[0].AsI64().CompareTo(arguments[1].AsI64()),
        "str" => string.CompareOrdinal(Text(arguments[0]), Text(arguments[1])),
        _ => throw new InvalidOperationException($"Type '{type}' has no total order.")
    };

    private static string Text(VarnValue value) => value.Value as string
        ?? throw new InvalidOperationException($"Expected str, got {value.Type}.");

    private static void RegisterI64(VarnModuleBuilder builder, string name, Func<long, long, long> operation)
    {
        builder.Function(
            new VarnFunctionSignature(name, [VarnType.I64, VarnType.I64], VarnType.I64),
            (_, arguments, _) => ValueTask.FromResult(VarnValue.From(operation(arguments[0].AsI64(), arguments[1].AsI64()))));
    }

    private static void RegisterF64(VarnModuleBuilder builder, string name, Func<double, double, double> operation)
    {
        builder.Function(
            new VarnFunctionSignature(name, [VarnType.F64, VarnType.F64], VarnType.F64),
            (_, arguments, _) => ValueTask.FromResult(VarnValue.From(operation(arguments[0].AsF64(), arguments[1].AsF64()))));
    }

    private static void RegisterPredicate(
        VarnModuleBuilder builder,
        string name,
        VarnType operandType,
        Func<IReadOnlyList<VarnValue>, bool> operation)
    {
        builder.Function(
            new VarnFunctionSignature(name, [operandType, operandType], VarnType.Bool),
            (_, arguments, _) => ValueTask.FromResult(VarnValue.From(operation(arguments))));
    }

    private static void RegisterStringPredicate(
        VarnModuleBuilder builder,
        string name,
        Func<string, string, bool> operation)
    {
        builder.Function(
            new VarnFunctionSignature(name, [VarnType.String, VarnType.String], VarnType.Bool),
            (_, arguments, _) => ValueTask.FromResult(
                VarnValue.From(operation(Text(arguments[0]), Text(arguments[1])))));
    }
}
