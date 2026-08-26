using System.Globalization;
using Varn.Syntax;

namespace Varn.ModuleSdk;

public readonly record struct VarnValue(VarnType Type, object? Value)
{
    public const int MaxListElements = 1024;

    public static VarnValue From(long value) => new(VarnType.I64, value);
    public static VarnValue From(double value) => new(VarnType.F64, value);
    public static VarnValue From(bool value) => new(VarnType.Bool, value);
    public static VarnValue From(string value) => new(VarnType.String, value);
    public static VarnValue Some(VarnValue value) =>
        new(VarnType.Optional(ValidateOptionalElementType(value.Type)), value);

    public static VarnValue None(VarnType elementType) =>
        new(VarnType.Optional(ValidateOptionalElementType(elementType)), null);

    public static VarnValue FromList(VarnType elementType, IEnumerable<VarnValue> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        elementType = ValidateListElementType(elementType);
        var values = elements.ToArray();
        if (values.Length > MaxListElements)
        {
            throw new ArgumentException($"A Varn list cannot contain more than {MaxListElements} elements.", nameof(elements));
        }

        if (values.Any(value => value.Type != elementType))
        {
            throw new ArgumentException($"Every list value must have type '{elementType}'.", nameof(elements));
        }

        return new VarnValue(VarnType.List(elementType), Array.AsReadOnly(values));
    }

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

    public IReadOnlyList<VarnValue> AsList() => Type.IsList && Value is IReadOnlyList<VarnValue> values
        ? values
        : throw new InvalidOperationException($"Expected list, got {Type}.");

    private static VarnType ValidateOptionalElementType(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (elementType != VarnType.I64 && elementType != VarnType.F64 &&
            elementType != VarnType.Bool && elementType != VarnType.String)
        {
            throw new ArgumentException($"Type '{elementType}' cannot be an optional element type.", nameof(elementType));
        }

        return elementType;
    }

    private static VarnType ValidateListElementType(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (elementType != VarnType.I64 && elementType != VarnType.F64 &&
            elementType != VarnType.Bool && elementType != VarnType.String)
        {
            throw new ArgumentException($"Type '{elementType}' cannot be a list element type.", nameof(elementType));
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

        if (Type.IsList)
        {
            return $"list[{Type.ListElementType}]({string.Join(",", AsList().Select(static value => value.ToCanonicalString()))})";
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
