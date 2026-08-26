using System.Text.Json.Serialization;

namespace Varn.Runtime;

public sealed record VarnSpanResponse(
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("column")] int Column);

public sealed record VarnDiagnosticResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("span")] VarnSpanResponse Span);

public sealed record VarnValueResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] object? Value);

public sealed record VarnRecordFieldResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] VarnValueResponse Value);

public sealed record VarnCheckResponse(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<VarnDiagnosticResponse> Diagnostics);

public sealed record VarnInspectionResponse(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("canonical")] string? Canonical,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<VarnDiagnosticResponse> Diagnostics);

public sealed record VarnRunResponse(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("steps")] long Steps,
    [property: JsonPropertyName("returnValue")] VarnValueResponse? ReturnValue,
    [property: JsonPropertyName("output")] string Output,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<VarnDiagnosticResponse> Diagnostics);
