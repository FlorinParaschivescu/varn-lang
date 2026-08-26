using System.ComponentModel;
using ModelContextProtocol.Server;
using Varn.Adapter;
using Varn.Runtime;

namespace Varn.ToolHost;

[McpServerToolType]
public sealed class VarnMcpTools(VarnToolService service)
{
    [McpServerTool(
        Name = "varn_check",
        Title = "Check Varn source",
        UseStructuredContent = true,
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Parse and statically validate Varn source without executing it, and report the structured input and result contract its entry point declares.")]
    public VarnCheckResponse Check(
        [Description("Complete Varn program source to validate. Include budget[steps=...] and at least one fn ... end block.")] string source) =>
        service.Check(source);

    [McpServerTool(
        Name = "varn_inspect",
        Title = "Inspect Varn source",
        UseStructuredContent = true,
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Validate Varn source and return its deterministic canonical structure.")]
    public VarnInspectionResponse Inspect(
        [Description("Complete Varn program source to inspect after it passes varn_check.")] string source) =>
        service.Inspect(source);

    [McpServerTool(
        Name = "varn_run",
        Title = "Run Varn source",
        UseStructuredContent = true,
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Validate and execute Varn source with explicit host capability and resource ceilings, optionally against structured host input.")]
    public ValueTask<VarnRunResponse> RunAsync(
        [Description("Complete checked Varn program source to execute.")] string source,
        [Description("Exact host capabilities to grant. Pass an empty array to grant none.")] string[] allowedCapabilities,
        [Description("Positive execution step ceiling, at most 1000000.")] long maxSteps,
        [Description("Positive captured-output character ceiling, at most 1000000.")] int maxOutputCharacters,
        CancellationToken cancellationToken,
        [Description("JSON object matching the input contract varn_check reports, or omit it when the program declares no input. Supply data here instead of writing it into the source, so one program stays reusable.")] string? input = null) =>
        service.RunAsync(source, allowedCapabilities, maxSteps, maxOutputCharacters, input, cancellationToken);
}
