namespace Varn.Syntax;

public readonly record struct SourceSpan(int Line, int Column)
{
    public override string ToString() => $"{Line}:{Column}";
}

public sealed record Diagnostic(string Code, string Message, SourceSpan Span)
{
    public override string ToString() => $"{Code} at {Span}: {Message}";
}

public sealed record VarnType(string Name)
{
    public static readonly VarnType I64 = new("i64");
    public static readonly VarnType F64 = new("f64");
    public static readonly VarnType Bool = new("bool");
    public static readonly VarnType String = new("str");
    public static readonly VarnType Null = new("null");
    public static readonly VarnType Any = new("any");

    public bool IsScalar =>
        this == I64 || this == F64 || this == Bool || this == String;

    public bool IsOptional => Name.EndsWith("?", StringComparison.Ordinal);

    public bool IsList => Name.StartsWith("list[", StringComparison.Ordinal) && Name.EndsWith(']');

    public VarnType? OptionalElementType => IsOptional ? Parse(Name[..^1]) : null;

    public VarnType? ListElementType => IsList ? Parse(Name[5..^1]) : null;

    public static VarnType Optional(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return new VarnType($"{elementType.Name}?");
    }

    public static VarnType List(VarnType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return new VarnType($"list[{elementType.Name}]");
    }

    public static VarnType Parse(string name)
    {
        if (name.EndsWith("?", StringComparison.Ordinal))
        {
            return Optional(Parse(name[..^1]));
        }

        if (name.StartsWith("list[", StringComparison.Ordinal) && name.EndsWith(']'))
        {
            return List(Parse(name[5..^1]));
        }

        return name switch
        {
            "i64" => I64,
            "f64" => F64,
            "bool" => Bool,
            "str" => String,
            "null" => Null,
            "any" => Any,
            _ => new VarnType(name)
        };
    }

    public override string ToString() => Name;
}
