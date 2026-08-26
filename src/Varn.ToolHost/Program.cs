using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Varn.Adapter;
using Varn.ToolHost;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton<VarnToolService>();
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "varn",
            Title = "Varn local tool host",
            Version = "0.1.0"
        };
        options.ServerInstructions =
            "Use varn_check before varn_inspect or varn_run. Run only when execution is needed. " +
            "Every run must include explicit allowedCapabilities, maxSteps, and maxOutputCharacters. " +
            "Grant the smallest capability set; this host exposes built-in modules only. " +
            "Varn uses newline-delimited statements and end-delimited blocks, not braces. Return is ret. " +
            "Minimal shape: budget[steps=100] then fn main()->i64, statements, ret 0, end. " +
            "Use let @0:i64 0 for an immutable slot, var @0:i64 0 for a mutable slot, " +
            "set @0 add(@0,1) to assign, and loop @1:i64 from 0 to 4 max 4 ... end for a bounded loop. " +
            "Use i64? for an optional type, some(42) or none[i64] to construct it, and " +
            "if let @1:i64 @0 ... else ... end to safely bind a present value. " +
            "Use list[i64](1,2,3) for a homogeneous list, list.length(@0) for its length, " +
            "list.get(@0,1) for an i64?, and each @1:i64 in @0 max 3 ... end for bounded traversal.";
    })
    .WithStdioServerTransport()
    .WithTools<VarnMcpTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
