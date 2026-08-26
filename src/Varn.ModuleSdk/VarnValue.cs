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

    /// <summary>A successful <c>result[T]</c> carrying <paramref name="value"/>.</summary>
    public static VarnValue Ok(VarnValue value) =>
        new(VarnType.Result(ValidateResultValueType(value.Type)), new VarnResultValue(true, value));

    /// <summary>A failed <c>result[T]</c> carrying an ordinary <c>str</c> message.</summary>
    public static VarnValue Err(VarnType valueType, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new VarnValue(
            VarnType.Result(ValidateResultValueType(valueType)),
            new VarnResultValue(false, From(message)));
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

    /// <summary>
    /// A type that a list, optional, record field, or result may contain: a scalar, or a named
    /// record type. The SDK cannot tell a declared record from a typo, so it accepts any name that
    /// is not a built-in or another type constructor; the checker rejects names no program declared.
    /// </summary>
    public static bool IsContainedType(VarnType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.IsScalar || (!type.IsList && !type.IsOptional && !type.IsResult &&
            type != VarnType.Null && type != VarnType.Any);
    }

    /// <summary>
    /// A record field holds a contained type, an optional of one, or a list of one. Nesting stops
    /// there: no lists of lists, no optional optionals, no results in fields.
    /// </summary>
    public static bool IsSupportedFieldType(VarnType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsOptional)
        {
            return IsContainedType(type.OptionalElementType!);
        }

        return type.IsList ? IsContainedType(type.ListElementType!) : IsContainedType(type);
    }

    public static VarnValue Null => new(VarnType.Null, null);

    public bool IsSome => Type.IsOptional && Value is VarnValue;

    public bool IsRecord => Value is VarnRecordValue;

    public bool IsResult => Value is VarnResultValue;

    public bool IsOk => Value is VarnResultValue { IsOk: true };

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

    public VarnResultValue AsResult() => Value is VarnResultValue result
        ? result
        : throw new InvalidOperationException($"Expected result, got {Type}.");

    /// <summary>
    /// A result may carry a scalar or a record. Lists, optionals, and nested results are rejected
    /// here; the checker separately rejects a record name the program never declared.
    /// </summary>
    private static VarnType ValidateResultValueType(VarnType valueType)
    {
        ArgumentNullException.ThrowIfNull(valueType);
        return IsContainedType(valueType)
            ? valueType
            : throw new ArgumentException($"Type '{valueType}' cannot be a result value type.", nameof(valueType));
    }

    private static VarnType ValidateOptionalElementType(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return IsContainedType(elementType)
            ? elementType
            : throw new ArgumentException($"Type '{elementType}' cannot be an optional element type.", nameof(elementType));
    }

    private static VarnType ValidateListElementType(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return IsContainedType(elementType)
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

        if (Value is VarnResultValue result)
        {
            return result.IsOk
                ? $"ok({result.Value.ToCanonicalString()})"
                : $"err[{Type.ResultValueType}]({result.Value.ToCanonicalString()})";
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

/// <summary>
/// Either the success value of a <c>result[T]</c> or its <c>str</c> failure message. Both sides are
/// ordinary Varn values, and a program must inspect which side is present before reading either.
/// </summary>
public sealed class VarnResultValue
{
    internal VarnResultValue(bool isOk, VarnValue value)
    {
        IsOk = isOk;
        Value = value;
    }

    public bool IsOk { get; }

    public VarnValue Value { get; }
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
