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

public sealed record VarnResultResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("value")] VarnValueResponse? Value,
    [property: JsonPropertyName("error")] VarnValueResponse? Error);

public sealed record VarnRecordFieldResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] VarnValueResponse Value);

public sealed record VarnFieldResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type);

public sealed record VarnRecordContractResponse(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("fields")] IReadOnlyList<VarnFieldResponse> Fields);

public sealed record VarnContractResponse(
    [property: JsonPropertyName("input")] VarnRecordContractResponse? Input,
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("records")] IReadOnlyList<VarnRecordContractResponse> Records);

public sealed record VarnCheckResponse(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<VarnDiagnosticResponse> Diagnostics,
    [property: JsonPropertyName("contract")] VarnContractResponse? Contract = null);

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
