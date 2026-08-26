using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.Modules.Standard;

public sealed class CoreModule : IVarnModule
{
    public string Name => "varn.core";

    public void Register(VarnModuleBuilder builder)
    {
        RegisterI64(builder, "add", static (left, right) => checked(left + right));
        RegisterI64(builder, "sub", static (left, right) => checked(left - right));
        RegisterI64(builder, "mul", static (left, right) => checked(left * right));
        RegisterI64(builder, "div", static (left, right) => left / right);

        RegisterF64(builder, "add", static (left, right) => left + right);
        RegisterF64(builder, "sub", static (left, right) => left - right);
        RegisterF64(builder, "mul", static (left, right) => left * right);
        RegisterF64(builder, "div", static (left, right) => left / right);

        RegisterComparison(builder, VarnType.I64, "lt", static arguments => arguments[0].AsI64() < arguments[1].AsI64());
        RegisterComparison(builder, VarnType.F64, "lt", static arguments => arguments[0].AsF64() < arguments[1].AsF64());
        RegisterComparison(builder, VarnType.I64, "eq", static arguments => arguments[0].AsI64() == arguments[1].AsI64());
        RegisterComparison(builder, VarnType.F64, "eq", static arguments => arguments[0].AsF64() == arguments[1].AsF64());
        RegisterComparison(builder, VarnType.Bool, "eq", static arguments => Equals(arguments[0].Value, arguments[1].Value));
        RegisterComparison(builder, VarnType.String, "eq", static arguments => Equals(arguments[0].Value, arguments[1].Value));
    }

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

    private static void RegisterComparison(
        VarnModuleBuilder builder,
        VarnType operandType,
        string name,
        Func<IReadOnlyList<VarnValue>, bool> operation)
    {
        builder.Function(
            new VarnFunctionSignature(name, [operandType, operandType], VarnType.Bool),
            (_, arguments, _) => ValueTask.FromResult(VarnValue.From(operation(arguments))));
    }
}
