using System.Text.Json;
using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.Runtime;

public static class VarnJsonFormatter
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static VarnCheckResponse CreateCheckResponse(VarnCheckResult result) =>
        new(
            SchemaVersion,
            "check",
            result.IsValid,
            MapDiagnostics(result.Diagnostics),
            result.IsValid ? MapContract(result.Program) : null);

    public static VarnInspectionResponse CreateInspectionResponse(VarnCheckResult result, string? canonical) =>
        new(
            SchemaVersion,
            "inspect",
            result.IsValid,
            canonical,
            MapDiagnostics(result.Diagnostics));

    public static VarnRunResponse CreateRunResponse(VarnRunResult result, string output) =>
        new(
            SchemaVersion,
            "run",
            result.IsSuccess,
            result.ExitCode,
            result.Steps,
            result.ReturnValue is { } value ? MapValue(value) : null,
            output,
            MapDiagnostics(result.Diagnostics));

    public static VarnCheckResponse CreateCliErrorResponse(string? command, string message) =>
        new(
            SchemaVersion,
            command ?? "unknown",
            false,
            [new VarnDiagnosticResponse("VARN0001", message, new VarnSpanResponse(0, 0))]);

    public static string FormatCheck(VarnCheckResult result) =>
        Serialize(CreateCheckResponse(result));

    public static string FormatInspection(VarnCheckResult result, string? canonical) =>
        Serialize(CreateInspectionResponse(result, canonical));

    public static string FormatRun(VarnRunResult result, string output) =>
        Serialize(CreateRunResponse(result, output));

    public static string FormatCliError(string? command, string message) =>
        Serialize(CreateCliErrorResponse(command, message));

    private static string Serialize<T>(T response) =>
        JsonSerializer.Serialize(response, SerializerOptions);

    private static VarnContractResponse MapContract(ProgramSyntax program)
    {
        var input = VarnProgramContract.InputShape(program);
        return new VarnContractResponse(
            input is null
                ? null
                : new VarnRecordContractResponse(
                    input.Name,
                    [.. input.Fields.Select(static field => new VarnFieldResponse(field.Name, field.Type.Name))]),
            VarnProgramContract.ResultType(program)?.Name ?? VarnType.I64.Name);
    }

    private static IReadOnlyList<VarnDiagnosticResponse> MapDiagnostics(IReadOnlyList<Diagnostic> diagnostics) =>
        diagnostics.Select(static diagnostic =>
            new VarnDiagnosticResponse(
                diagnostic.Code,
                diagnostic.Message,
                new VarnSpanResponse(diagnostic.Span.Line, diagnostic.Span.Column)))
            .ToArray();

    private static VarnValueResponse MapValue(VarnValue value)
    {
        if (value.IsRecord)
        {
            var record = value.AsRecord();
            return new VarnValueResponse(
                value.Type.Name,
                record.Shape.Fields
                    .Select((field, index) => new VarnRecordFieldResponse(field.Name, MapValue(record.Values[index])))
                    .ToArray());
        }

        if (value.Type.IsOptional)
        {
            return new VarnValueResponse(value.Type.Name, value.IsSome ? MapValue(value.AsOptionalValue()) : null);
        }

        return value.Type.IsList
            ? new VarnValueResponse(value.Type.Name, value.AsList().Select(MapValue).ToArray())
            : new VarnValueResponse(value.Type.Name, value.Value);
    }
}
