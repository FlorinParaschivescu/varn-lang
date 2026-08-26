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

    public static VarnValue FromRecord(VarnRecordShape shape, IEnumerable<VarnValue> fieldValues)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(fieldValues);
        var values = fieldValues.ToArray();
        if (values.Length != shape.Fields.Count)
        {
            throw new ArgumentException(
                $"Record '{shape.Name}' declares {shape.Fields.Count} fields, got {values.Length} values.",
                nameof(fieldValues));
        }

        for (var index = 0; index < values.Length; index++)
        {
            var field = shape.Fields[index];
            if (!IsSupportedFieldType(field.Type))
            {
                throw new ArgumentException(
                    $"Type '{field.Type}' cannot be a record field type.",
                    nameof(shape));
            }

            if (values[index].Type != field.Type)
            {
                throw new ArgumentException(
                    $"Field '{shape.Name}.{field.Name}' requires type '{field.Type}', got '{values[index].Type}'.",
                    nameof(fieldValues));
            }
        }

        return new VarnValue(shape.Type, new VarnRecordValue(shape, Array.AsReadOnly(values)));
    }

    public static bool IsSupportedFieldType(VarnType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsOptional)
        {
            return type.OptionalElementType!.IsScalar;
        }

        return type.IsList ? type.ListElementType!.IsScalar : type.IsScalar;
    }

    public static VarnValue Null => new(VarnType.Null, null);

    public bool IsSome => Type.IsOptional && Value is VarnValue;

    public bool IsRecord => Value is VarnRecordValue;

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

    public VarnRecordValue AsRecord() => Value is VarnRecordValue record
        ? record
        : throw new InvalidOperationException($"Expected record, got {Type}.");

    private static VarnType ValidateOptionalElementType(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return elementType.IsScalar
            ? elementType
            : throw new ArgumentException($"Type '{elementType}' cannot be an optional element type.", nameof(elementType));
    }

    private static VarnType ValidateListElementType(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return elementType.IsScalar
            ? elementType
            : throw new ArgumentException($"Type '{elementType}' cannot be a list element type.", nameof(elementType));
    }

    public string ToCanonicalString()
    {
        if (Value is VarnRecordValue record)
        {
            var fields = record.Shape.Fields
                .Select((field, index) => $"{field.Name}={record.Values[index].ToCanonicalString()}");
            return $"{record.Shape.Name}({string.Join(",", fields)})";
        }

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

public sealed class VarnRecordValue
{
    internal VarnRecordValue(VarnRecordShape shape, IReadOnlyList<VarnValue> values)
    {
        Shape = shape;
        Values = values;
    }

    public VarnRecordShape Shape { get; }

    public IReadOnlyList<VarnValue> Values { get; }

    public VarnValue GetField(string name)
    {
        var index = Shape.IndexOf(name);
        return index >= 0
            ? Values[index]
            : throw new InvalidOperationException($"Record '{Shape.Name}' does not declare field '{name}'.");
    }
}
