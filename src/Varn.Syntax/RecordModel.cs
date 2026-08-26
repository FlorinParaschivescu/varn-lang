namespace Varn.Syntax;

public sealed record VarnRecordField(string Name, VarnType Type);

public sealed record VarnRecordShape
{
    public VarnRecordShape(string name, IReadOnlyList<VarnRecordField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(fields);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            ArgumentNullException.ThrowIfNull(field);
            if (string.IsNullOrWhiteSpace(field.Name) || !seen.Add(field.Name))
            {
                throw new ArgumentException($"Record '{name}' must declare unique non-empty field names.", nameof(fields));
            }
        }

        Name = name;
        Fields = [.. fields];
    }

    public string Name { get; }

    public IReadOnlyList<VarnRecordField> Fields { get; }

    public VarnType Type => new(Name);

    public int IndexOf(string field)
    {
        for (var index = 0; index < Fields.Count; index++)
        {
            if (string.Equals(Fields[index].Name, field, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public override string ToString() =>
        $"{Name}({string.Join(",", Fields.Select(static field => $"{field.Name}:{field.Type}"))})";
}
