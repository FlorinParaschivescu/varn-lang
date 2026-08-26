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

    public static VarnInputBinding Bind(VarnRecordShape? shape, string? input)
    {
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

        var unsupported = shape.Fields
            .Where(static field => !VarnValue.IsSupportedFieldType(field.Type))
            .ToArray();
        if (unsupported.Length > 0)
        {
            return new VarnInputBinding(
                null,
                [.. unsupported.Select(field => Diagnose(
                    "VARN6008",
                    $"Field '{field.Name}' declares type {field.Type}, which cannot be supplied as host input."))]);
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
            var properties = ReadProperties(shape, document.RootElement, diagnostics);
            var values = new VarnValue[shape.Fields.Count];
            for (var index = 0; index < shape.Fields.Count; index++)
            {
                var field = shape.Fields[index];
                if (!properties.TryGetValue(field.Name, out var element))
                {
                    diagnostics.Add(Diagnose(
                        "VARN6007",
                        $"Input is missing field '{field.Name}' of type {field.Type} required by '{shape.Name}'."));
                    continue;
                }

                values[index] = BindValue(field.Type, element, field.Name, diagnostics);
            }

            return diagnostics.Count > 0
                ? new VarnInputBinding(null, diagnostics)
                : new VarnInputBinding(VarnValue.FromRecord(shape, values), []);
        }
    }

    private static Dictionary<string, JsonElement> ReadProperties(
        VarnRecordShape shape,
        JsonElement root,
        List<Diagnostic> diagnostics)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (shape.IndexOf(property.Name) < 0)
            {
                diagnostics.Add(Diagnose(
                    "VARN6005",
                    $"Input sets field '{property.Name}', which record '{shape.Name}' does not declare."));
                continue;
            }

            if (!properties.TryAdd(property.Name, property.Value))
            {
                diagnostics.Add(Diagnose(
                    "VARN6006",
                    $"Input sets field '{property.Name}' more than once."));
            }
        }

        return properties;
    }

    /// <summary>
    /// Binds one value. The caller guarantees <paramref name="type"/> is a supported field type, so
    /// every placeholder returned alongside a diagnostic still has the declared type and keeps the
    /// surrounding list or record constructible until the diagnostics are reported.
    /// </summary>
    private static VarnValue BindValue(
        VarnType type,
        JsonElement element,
        string path,
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
            var contained = BindScalar(elementType, element, path, diagnostics);
            return diagnostics.Count > before ? VarnValue.None(elementType) : VarnValue.Some(contained);
        }

        return type.IsList
            ? BindList(type, element, path, diagnostics)
            : BindScalar(type, element, path, diagnostics);
    }

    private static VarnValue BindList(
        VarnType type,
        JsonElement element,
        string path,
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
            values.Add(BindScalar(elementType, item, $"{path}[{index}]", diagnostics));
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
                throw new InvalidOperationException($"Type '{type}' is not a bindable scalar.");
        }
    }

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
