using System.Globalization;
using Varn.Syntax;

namespace Varn.ModuleSdk;

public readonly record struct VarnValue(VarnType Type, object? Value)
{
    public static VarnValue From(long value) => new(VarnType.I64, value);
    public static VarnValue From(double value) => new(VarnType.F64, value);
    public static VarnValue From(bool value) => new(VarnType.Bool, value);
    public static VarnValue From(string value) => new(VarnType.String, value);
    public static VarnValue Null => new(VarnType.Null, null);

    public long AsI64() => Value is long value
        ? value
        : throw new InvalidOperationException($"Expected i64, got {Type}.");

    public double AsF64() => Value is double value
        ? value
        : throw new InvalidOperationException($"Expected f64, got {Type}.");

    public bool AsBool() => Value is bool value
        ? value
        : throw new InvalidOperationException($"Expected bool, got {Type}.");

    public string ToCanonicalString() => Type.Name switch
    {
        "null" => "null",
        "bool" => (bool)Value! ? "true" : "false",
        "f64" => ((double)Value!).ToString("R", CultureInfo.InvariantCulture),
        "i64" => ((long)Value!).ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "null"
    };
}
