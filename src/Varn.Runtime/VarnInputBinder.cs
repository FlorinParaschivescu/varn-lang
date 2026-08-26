using System.Text.Json;
using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.Runtime;

/// <summary>
/// Binds host-supplied JSON to the closed record a program declares as its input. Binding is total
/// and happens before execution: either every declared field is present with an exactly matching
/// type, or the input is rejected with a diagnostic naming the offending path.
/// </summary>
public static class VarnInputBinder
{
    public const int MaximumInputCharacters = 1_000_000;

    private static readonly SourceSpan HostInputSpan = new(0, 0);

    public static VarnInputBinding Bind(
        VarnRecordShape? shape,
        IReadOnlyDictionary<string, VarnRecordShape> records,
        string? input)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (shape is null)
        {
            return input is null
                ? new VarnInputBinding(null, [])
                : Rejected("VARN6001", "This program declares no input, so no input may be supplied.");
        }

        if (input is null)
        {
            return Rejected("VARN6000", $"This program requires input of type '{shape.Name}', but none was supplied.");
        }

        if (input.Length > MaximumInputCharacters)
        {
            return Rejected(
                "VARN6002",
                $"Input exceeds the host ceiling of {MaximumInputCharacters} characters.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(input);
        }
        catch (JsonException exception)
        {
            return Rejected("VARN6003", $"Input is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Rejected(
                    "VARN6004",
                    $"Input must be a JSON object describing '{shape.Name}', got {Describe(document.RootElement.ValueKind)}.");
            }

            var diagnostics = new List<Diagnostic>();
            var value = BindRecord(shape, document.RootElement, shape.Name, records, diagnostics);
            return diagnostics.Count > 0
                ? new VarnInputBinding(null, diagnostics)
                : new VarnInputBinding(value, []);
        }
    }

    private static VarnValue BindRecord(
        VarnRecordShape shape,
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, VarnRecordShape> records,
        List<Diagnostic> diagnostics)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnose(
                "VARN6008",
                $"Input field '{path}' requires {shape.Name}, got {Describe(element.ValueKind)}."));
            return Default(shape.Type, records);
        }

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (shape.IndexOf(property.Name) < 0)
            {
                diagnostics.Add(Diagnose(
                    "VARN6005",
                    $"Input sets field '{Join(path, property.Name)}', which record '{shape.Name}' does not declare."));
                continue;
            }

            if (!properties.TryAdd(property.Name, property.Value))
            {
                diagnostics.Add(Diagnose(
                    "VARN6006",
                    $"Input sets field '{Join(path, property.Name)}' more than once."));
            }
        }

        var values = new VarnValue[shape.Fields.Count];
        for (var index = 0; index < shape.Fields.Count; index++)
        {
            var field = shape.Fields[index];
            if (!properties.TryGetValue(field.Name, out var fieldElement))
            {
                diagnostics.Add(Diagnose(
                    "VARN6007",
                    $"Input is missing field '{Join(path, field.Name)}' of type {field.Type} required by '{shape.Name}'."));
                values[index] = Default(field.Type, records);
                continue;
            }

            values[index] = BindValue(field.Type, fieldElement, Join(path, field.Name), records, diagnostics);
        }

        return VarnValue.FromRecord(shape, values);
    }

    private static VarnValue BindValue(
        VarnType type,
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, VarnRecordShape> records,
        List<Diagnostic> diagnostics)
    {
        if (type.IsOptional)
        {
            var elementType = type.OptionalElementType!;
            if (element.ValueKind == JsonValueKind.Null)
            {
                return VarnValue.None(elementType);
            }

            var before = diagnostics.Count;
            var contained = BindContained(elementType, element, path, records, diagnostics);
            return diagnostics.Count > before ? VarnValue.None(elementType) : VarnValue.Some(contained);
        }

        if (type.IsList)
        {
            return BindList(type, element, path, records, diagnostics);
        }

        return BindContained(type, element, path, records, diagnostics);
    }

    private static VarnValue BindContained(
        VarnType type,
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, VarnRecordShape> records,
        List<Diagnostic> diagnostics) =>
        records.TryGetValue(type.Name, out var shape)
            ? BindRecord(shape, element, path, records, diagnostics)
            : BindScalar(type, element, path, diagnostics);

    private static VarnValue BindList(
        VarnType type,
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, VarnRecordShape> records,
        List<Diagnostic> diagnostics)
    {
        var elementType = type.ListElementType!;
        if (element.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Mismatch(type, element, path));
            return VarnValue.FromList(elementType, []);
        }

        if (element.GetArrayLength() > VarnValue.MaxListElements)
        {
            diagnostics.Add(Diagnose(
                "VARN6010",
                $"Input field '{path}' has {element.GetArrayLength()} elements, more than the {VarnValue.MaxListElements} allowed."));
            return VarnValue.FromList(elementType, []);
        }

        var before = diagnostics.Count;
        var values = new List<VarnValue>(element.GetArrayLength());
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            values.Add(BindContained(elementType, item, $"{path}[{index}]", records, diagnostics));
            index++;
        }

        return diagnostics.Count > before
            ? VarnValue.FromList(elementType, [])
            : VarnValue.FromList(elementType, values);
    }

    private static VarnValue BindScalar(
        VarnType type,
        JsonElement element,
        string path,
        List<Diagnostic> diagnostics)
    {
        switch (type.Name)
        {
            case "i64":
                if (element.ValueKind != JsonValueKind.Number)
                {
                    diagnostics.Add(Mismatch(type, element, path));
                    return VarnValue.From(0L);
                }

                if (!element.TryGetInt64(out var integer))
                {
                    diagnostics.Add(Diagnose(
                        "VARN6009",
                        $"Input field '{path}' value {element.GetRawText()} is not an i64; i64 requires a whole number between {long.MinValue} and {long.MaxValue}."));
                    return VarnValue.From(0L);
                }

                return VarnValue.From(integer);
            case "f64":
                if (element.ValueKind != JsonValueKind.Number)
                {
                    diagnostics.Add(Mismatch(type, element, path));
                    return VarnValue.From(0d);
                }

                if (!element.TryGetDouble(out var floating) || double.IsNaN(floating) || double.IsInfinity(floating))
                {
                    diagnostics.Add(Diagnose(
                        "VARN6009",
                        $"Input field '{path}' value {element.GetRawText()} is not a finite f64."));
                    return VarnValue.From(0d);
                }

                return VarnValue.From(floating);
            case "bool":
                if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    diagnostics.Add(Mismatch(type, element, path));
                    return VarnValue.From(false);
                }

                return VarnValue.From(element.GetBoolean());
            case "str":
                if (element.ValueKind != JsonValueKind.String)
                {
                    diagnostics.Add(Mismatch(type, element, path));
                    return VarnValue.From(string.Empty);
                }

                return VarnValue.From(element.GetString()!);
            default:
                diagnostics.Add(Diagnose(
                    "VARN6008",
                    $"Input field '{path}' declares type {type}, which cannot be supplied as host input."));
                return VarnValue.From(0L);
        }
    }

    /// <summary>
    /// A type-correct stand-in used when a field failed to bind, so the surrounding record or list
    /// still constructs and every remaining field is checked before the input is rejected. Records
    /// are acyclic by the time binding runs, so this recursion terminates.
    /// </summary>
    private static VarnValue Default(VarnType type, IReadOnlyDictionary<string, VarnRecordShape> records)
    {
        if (type.IsOptional)
        {
            return VarnValue.None(type.OptionalElementType!);
        }

        if (type.IsList)
        {
            return VarnValue.FromList(type.ListElementType!, []);
        }

        if (records.TryGetValue(type.Name, out var shape))
        {
            return VarnValue.FromRecord(shape, [.. shape.Fields.Select(field => Default(field.Type, records))]);
        }

        return type.Name switch
        {
            "f64" => VarnValue.From(0d),
            "bool" => VarnValue.From(false),
            "str" => VarnValue.From(string.Empty),
            _ => VarnValue.From(0L)
        };
    }

    private static string Join(string path, string field) => $"{path}.{field}";

    private static Diagnostic Mismatch(VarnType type, JsonElement element, string path) =>
        Diagnose(
            "VARN6008",
            $"Input field '{path}' requires {type}, got {Describe(element.ValueKind)}.");

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        _ => "an undefined value"
    };

    private static VarnInputBinding Rejected(string code, string message) =>
        new(null, [Diagnose(code, message)]);

    private static Diagnostic Diagnose(string code, string message) =>
        new(code, message, HostInputSpan);
}

public sealed record VarnInputBinding(VarnValue? Value, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}
