using System.Globalization;
using Varn.Syntax;

namespace Varn.ModuleSdk;

public readonly record struct VarnValue(VarnType Type, object? Value)
{
    public static VarnValue From(long value) => new(VarnType.I64, value);
    public static VarnValue From(double value) => new(VarnType.F64, value);
    public static VarnValue From(bool value) => new(VarnType.Bool, value);
    public static VarnValue From(string value) => new(VarnType.String, value);
    public static VarnValue Some(VarnValue value) =>
        new(VarnType.Optional(ValidateOptionalElementType(value.Type)), value);

    public static VarnValue None(VarnType elementType) =>
        new(VarnType.Optional(ValidateOptionalElementType(elementType)), null);
    public static VarnValue Null => new(VarnType.Null, null);

    public bool IsSome => Type.IsOptional && Value is VarnValue;

    public long AsI64() => Value is long value
        ? value
        : throw new InvalidOperationException($"Expected i64, got {Type}.");

    public double AsF64() => Value is double value
        ? value
        : throw new InvalidOperationException($"Expected f64, got {Type}.");

    public bool AsBool() => Value is bool value
        ? value
        : throw new InvalidOperationException($"Expected bool, got {Type}.");

    public VarnValue AsOptionalValue() => Type.IsOptional && Value is VarnValue value
        ? value
        : throw new InvalidOperationException($"Expected a present optional, got {ToCanonicalString()}.");

    private static VarnType ValidateOptionalElementType(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (elementType.IsOptional || elementType == VarnType.Null || elementType == VarnType.Any)
        {
            throw new ArgumentException($"Type '{elementType}' cannot be an optional element type.", nameof(elementType));
        }

        return elementType;
    }

    public string ToCanonicalString()
    {
        if (Type.IsOptional)
        {
            return Value is VarnValue value
                ? $"some({value.ToCanonicalString()})"
                : $"none[{Type.OptionalElementType}]";
        }

        return Type.Name switch
        {
            "null" => "null",
            "bool" => (bool)Value! ? "true" : "false",
            "f64" => ((double)Value!).ToString("R", CultureInfo.InvariantCulture),
            "i64" => ((long)Value!).ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? "null"
        };
    }
}
