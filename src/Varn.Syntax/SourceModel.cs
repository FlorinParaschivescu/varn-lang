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

    public static VarnType Parse(string name) => name switch
    {
        "i64" => I64,
        "f64" => F64,
        "bool" => Bool,
        "str" => String,
        "null" => Null,
        "any" => Any,
        _ => new VarnType(name)
    };

    public override string ToString() => Name;
}
