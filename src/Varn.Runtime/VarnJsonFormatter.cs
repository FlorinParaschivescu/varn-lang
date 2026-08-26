using System.Text.Json;
using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.Runtime;

public static class VarnJsonFormatter
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string FormatCheck(VarnCheckResult result) =>
        JsonSerializer.Serialize(
            new CheckEnvelope(
                SchemaVersion,
                "check",
                result.IsValid,
                MapDiagnostics(result.Diagnostics)),
            SerializerOptions);

    public static string FormatInspection(VarnCheckResult result, string? canonical) =>
        JsonSerializer.Serialize(
            new InspectionEnvelope(
                SchemaVersion,
                "inspect",
                result.IsValid,
                canonical,
                MapDiagnostics(result.Diagnostics)),
            SerializerOptions);

    public static string FormatRun(VarnRunResult result, string output) =>
        JsonSerializer.Serialize(
            new RunEnvelope(
                SchemaVersion,
                "run",
                result.IsSuccess,
                result.ExitCode,
                result.Steps,
                result.ReturnValue is { } value ? MapValue(value) : null,
                output,
                MapDiagnostics(result.Diagnostics)),
            SerializerOptions);

    public static string FormatCliError(string? command, string message) =>
        JsonSerializer.Serialize(
            new CheckEnvelope(
                SchemaVersion,
                command ?? "unknown",
                false,
                [new DiagnosticEnvelope("VARN0001", message, new SpanEnvelope(0, 0))]),
            SerializerOptions);

    private static IReadOnlyList<DiagnosticEnvelope> MapDiagnostics(IReadOnlyList<Diagnostic> diagnostics) =>
        diagnostics.Select(static diagnostic =>
            new DiagnosticEnvelope(
                diagnostic.Code,
                diagnostic.Message,
                new SpanEnvelope(diagnostic.Span.Line, diagnostic.Span.Column)))
            .ToArray();

    private static ValueEnvelope MapValue(VarnValue value) =>
        new(value.Type.Name, value.Value);

    private sealed record CheckEnvelope(
        int SchemaVersion,
        string Command,
        bool Success,
        IReadOnlyList<DiagnosticEnvelope> Diagnostics);

    private sealed record InspectionEnvelope(
        int SchemaVersion,
        string Command,
        bool Success,
        string? Canonical,
        IReadOnlyList<DiagnosticEnvelope> Diagnostics);

    private sealed record RunEnvelope(
        int SchemaVersion,
        string Command,
        bool Success,
        int ExitCode,
        long Steps,
        ValueEnvelope? ReturnValue,
        string Output,
        IReadOnlyList<DiagnosticEnvelope> Diagnostics);

    private sealed record DiagnosticEnvelope(string Code, string Message, SpanEnvelope Span);

    private sealed record SpanEnvelope(int Line, int Column);

    private sealed record ValueEnvelope(string Type, object? Value);
}
