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
    [Description("Parse and statically validate Varn source without executing it.")]
    public VarnCheckResponse Check(
        [Description("Complete Varn source text to validate.")] string source) =>
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
        [Description("Complete Varn source text to inspect.")] string source) =>
        service.Inspect(source);

    [McpServerTool(
        Name = "varn_run",
        Title = "Run Varn source",
        UseStructuredContent = true,
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Validate and execute Varn source with explicit host capability and resource ceilings.")]
    public ValueTask<VarnRunResponse> RunAsync(
        [Description("Complete Varn source text to execute.")] string source,
        [Description("Exact host capabilities to grant. Pass an empty array to grant none.")] string[] allowedCapabilities,
        [Description("Positive execution step ceiling, at most 1000000.")] long maxSteps,
        [Description("Positive captured-output character ceiling, at most 1000000.")] int maxOutputCharacters,
        CancellationToken cancellationToken) =>
        service.RunAsync(source, allowedCapabilities, maxSteps, maxOutputCharacters, cancellationToken);
}
