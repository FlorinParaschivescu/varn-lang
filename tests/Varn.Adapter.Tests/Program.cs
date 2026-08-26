using System.Text.Json;
using ModelContextProtocol.Client;
using Varn.Adapter;

namespace Varn.Adapter.Tests;

public static class Program
{
    private const string HelloProgram = """
        cap[console.write]
        budget[steps=100]

        fn main()->i64 ![console]
            io.print(30)
            ret 0
        end
        """;

    public static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("adapter check returns schema-v1 diagnostics", CheckReturnsStableDiagnostics),
            ("adapter requires an explicit capability policy", RunRequiresExplicitCapabilities),
            ("adapter denies an ungranted program capability", RunDeniesMissingCapability),
            ("adapter captures successful execution", RunCapturesSuccessfulExecution),
            ("adapter enforces output ceilings", RunEnforcesOutputCeiling),
            ("MCP stdio host supports check-repair-run", McpHostSupportsCheckRepairRun)
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run().ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} adapter tests passed");
        return failures == 0 ? 0 : 1;
    }

    private static Task CheckReturnsStableDiagnostics()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                ret true
            end
            """;
        var response = new VarnToolService().Check(source);
        Assert(response.SchemaVersion == 1, "Expected schema version 1.");
        Assert(response.Command == "check", "Expected the check command.");
        Assert(!response.Success, "Expected invalid source to fail.");
        Assert(response.Diagnostics.Any(static diagnostic => diagnostic.Code == "VARN3008"), "Expected VARN3008.");
        return Task.CompletedTask;
    }

    private static async Task RunRequiresExplicitCapabilities()
    {
        var response = await new VarnToolService().RunAsync(HelloProgram, null, 100, 100).ConfigureAwait(false);
        Assert(!response.Success, "Expected a missing capability policy to fail.");
        AssertHasDiagnostic(response.Diagnostics, "VARN5002");
    }

    private static async Task RunDeniesMissingCapability()
    {
        var response = await new VarnToolService().RunAsync(HelloProgram, [], 100, 100).ConfigureAwait(false);
        Assert(!response.Success, "Expected the missing console grant to fail.");
        AssertHasDiagnostic(response.Diagnostics, "VARN4002");
    }

    private static async Task RunCapturesSuccessfulExecution()
    {
        var response = await new VarnToolService().RunAsync(
            HelloProgram,
            ["console.write"],
            100,
            100).ConfigureAwait(false);
        Assert(response.Success, FormatDiagnostics(response.Diagnostics));
        Assert(response.Output.Trim() == "30", $"Expected output 30, got '{response.Output}'.");
        var returnValue = response.ReturnValue
            ?? throw new InvalidOperationException("Expected a return value.");
        Assert(returnValue.Type == "i64", "Expected an i64 return type.");
        Assert(Convert.ToInt64(returnValue.Value) == 0, "Expected return value 0.");
        Assert(response.Steps > 0, "Expected positive step usage.");
    }

    private static async Task RunEnforcesOutputCeiling()
    {
        const string source = """
            cap[console.write]
            budget[steps=20]
            fn main()->i64 ![console]
                io.print("abcdef")
                ret 0
            end
            """;
        var response = await new VarnToolService().RunAsync(
            source,
            ["console.write"],
            20,
            3).ConfigureAwait(false);
        Assert(!response.Success, "Expected output truncation to fail the run.");
        Assert(response.Output == "abc", $"Expected three captured characters, got '{response.Output}'.");
        AssertHasDiagnostic(response.Diagnostics, "VARN5005");
    }

    private static async Task McpHostSupportsCheckRepairRun()
    {
        var executable = FindToolHostExecutable();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "Varn adapter integration test",
            Command = executable,
            Arguments = [],
            InheritEnvironmentVariables = false
        });

        await using var client = await McpClient.CreateAsync(transport).ConfigureAwait(false);
        Assert(
            client.ServerInstructions?.Contains("varn_check", StringComparison.Ordinal) is true,
            "Expected server workflow instructions.");

        var tools = await client.ListToolsAsync().ConfigureAwait(false);
        var names = tools.Select(static tool => tool.Name).Order(StringComparer.Ordinal).ToArray();
        Assert(
            names.SequenceEqual(["varn_check", "varn_inspect", "varn_run"]),
            $"Unexpected MCP tools: {string.Join(", ", names)}.");

        const string invalidSource = """
            budget[steps=20]
            fn main()->i64
                ret true
            end
            """;
        var checkResult = await client.CallToolAsync(
            "varn_check",
            new Dictionary<string, object?> { ["source"] = invalidSource }).ConfigureAwait(false);
        var check = StructuredRoot(checkResult.StructuredContent);
        Assert(!check.GetProperty("success").GetBoolean(), "Expected MCP check to reject invalid source.");
        Assert(
            check.GetProperty("diagnostics").EnumerateArray()
                .Any(static diagnostic => diagnostic.GetProperty("code").GetString() == "VARN3008"),
            "Expected MCP VARN3008 diagnostic.");

        const string repairedSource = """
            budget[steps=20]
            fn main()->i64
                ret 0
            end
            """;
        var runResult = await client.CallToolAsync(
            "varn_run",
            new Dictionary<string, object?>
            {
                ["source"] = repairedSource,
                ["allowedCapabilities"] = Array.Empty<string>(),
                ["maxSteps"] = 20L,
                ["maxOutputCharacters"] = 100
            }).ConfigureAwait(false);
        var run = StructuredRoot(runResult.StructuredContent);
        Assert(run.GetProperty("success").GetBoolean(), "Expected MCP run to accept repaired source.");
        Assert(run.GetProperty("returnValue").GetProperty("value").GetInt64() == 0, "Expected MCP return value 0.");
    }

    private static string FindToolHostExecutable()
    {
        var root = FindRepositoryRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        var fileName = OperatingSystem.IsWindows() ? "Varn.ToolHost.exe" : "Varn.ToolHost";
        var executable = Path.Combine(root, "src", "Varn.ToolHost", "bin", configuration, "net10.0", fileName);
        return File.Exists(executable)
            ? executable
            : throw new FileNotFoundException("The MCP tool host executable was not built.", executable);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Varn.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the Varn repository root.");
    }

    private static JsonElement StructuredRoot(object? structuredContent)
    {
        Assert(structuredContent is not null, "Expected MCP structuredContent.");
        return JsonSerializer.SerializeToElement(structuredContent).Clone();
    }

    private static void AssertHasDiagnostic(IReadOnlyList<Varn.Runtime.VarnDiagnosticResponse> diagnostics, string code) =>
        Assert(diagnostics.Any(diagnostic => diagnostic.Code == code), $"Expected {code}; got {FormatDiagnostics(diagnostics)}");

    private static string FormatDiagnostics(IEnumerable<Varn.Runtime.VarnDiagnosticResponse> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
