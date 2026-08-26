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
            "Grant the smallest capability set; this host exposes built-in modules only.";
    })
    .WithStdioServerTransport()
    .WithTools<VarnMcpTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
