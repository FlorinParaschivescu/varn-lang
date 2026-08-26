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
            ("MCP stdio host supports optional and list check-inspect-run", McpHostSupportsCheckRepairRun)
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
        Assert(
            client.ServerInstructions?.Contains("loop @1:i64 from 0 to 4 max 4", StringComparison.Ordinal) is true,
            "Expected compact Varn syntax guidance.");
        Assert(
            client.ServerInstructions?.Contains("if let @1:i64 @0", StringComparison.Ordinal) is true,
            "Expected compact optional syntax guidance.");
        Assert(
            client.ServerInstructions?.Contains("list[i64](1,2,3)", StringComparison.Ordinal) is true,
            "Expected compact typed-list syntax guidance.");
        Assert(
            client.ServerInstructions?.Contains("each @1:i64 in @0 max 3", StringComparison.Ordinal) is true,
            "Expected compact bounded list traversal guidance.");

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
            budget[steps=100]
            fn maybe(@0:bool)->i64?
                if @0
                    ret some(42)
                end
                ret none[i64]
            end
            fn main()->i64
                let @0:i64? maybe(true)
                if let @1:i64 @0
                    ret @1
                else
                    ret 0
                end
                ret 0
            end
            """;
        var repairedCheckResult = await client.CallToolAsync(
            "varn_check",
            new Dictionary<string, object?> { ["source"] = repairedSource }).ConfigureAwait(false);
        var repairedCheck = StructuredRoot(repairedCheckResult.StructuredContent);
        Assert(repairedCheck.GetProperty("success").GetBoolean(), "Expected MCP check to accept repaired optional source.");

        var inspectResult = await client.CallToolAsync(
            "varn_inspect",
            new Dictionary<string, object?> { ["source"] = repairedSource }).ConfigureAwait(false);
        var inspect = StructuredRoot(inspectResult.StructuredContent);
        Assert(inspect.GetProperty("success").GetBoolean(), "Expected MCP inspect to accept optional source.");
        Assert(
            inspect.GetProperty("canonical").GetString()?.Contains("J(@1:i64,V[@0])", StringComparison.Ordinal) is true,
            "Expected MCP canonical inspection to include safe optional extraction.");

        var runResult = await client.CallToolAsync(
            "varn_run",
            new Dictionary<string, object?>
            {
                ["source"] = repairedSource,
                ["allowedCapabilities"] = Array.Empty<string>(),
                ["maxSteps"] = 100L,
                ["maxOutputCharacters"] = 100
            }).ConfigureAwait(false);
        var run = StructuredRoot(runResult.StructuredContent);
        Assert(run.GetProperty("success").GetBoolean(), "Expected MCP run to accept repaired optional source.");
        Assert(run.GetProperty("returnValue").GetProperty("value").GetInt64() == 42, "Expected MCP optional value 42.");

        const string invalidListSource = """
            budget[steps=100]
            fn main()->i64
                let @0:list[i64] list[i64](1,2,3,4)
                each @1:bool in @0 max 4
                end
                ret 0
            end
            """;
        var invalidListResult = await client.CallToolAsync(
            "varn_check",
            new Dictionary<string, object?> { ["source"] = invalidListSource }).ConfigureAwait(false);
        var invalidList = StructuredRoot(invalidListResult.StructuredContent);
        Assert(
            invalidList.GetProperty("diagnostics").EnumerateArray()
                .Any(static diagnostic => diagnostic.GetProperty("code").GetString() == "VARN3033"),
            "Expected MCP to explain the list binding mismatch.");

        const string listSource = """
            budget[steps=200]
            fn main()->i64
                let @0:list[i64] list[i64](1,2,3,4)
                var @1:i64 0
                each @2:i64 in @0 max 4
                    set @1 add(@1,@2)
                end
                let @3:i64? list.get(@0,9)
                if let @4:i64 @3
                    ret -1
                else
                    ret @1
                end
                ret 0
            end
            """;
        var listCheckResult = await client.CallToolAsync(
            "varn_check",
            new Dictionary<string, object?> { ["source"] = listSource }).ConfigureAwait(false);
        var listCheck = StructuredRoot(listCheckResult.StructuredContent);
        Assert(listCheck.GetProperty("success").GetBoolean(), "Expected MCP check to accept the repaired list source.");

        var listInspectResult = await client.CallToolAsync(
            "varn_inspect",
            new Dictionary<string, object?> { ["source"] = listSource }).ConfigureAwait(false);
        var listInspect = StructuredRoot(listInspectResult.StructuredContent);
        var canonical = listInspect.GetProperty("canonical").GetString();
        Assert(canonical?.Contains("Q[i64](K[i64:1];K[i64:2];K[i64:3];K[i64:4])", StringComparison.Ordinal) is true,
            "Expected MCP canonical inspection to include the typed list.");
        Assert(canonical?.Contains("H(@2:i64,V[@0],4)", StringComparison.Ordinal) is true,
            "Expected MCP canonical inspection to include bounded list traversal.");

        var listRunResult = await client.CallToolAsync(
            "varn_run",
            new Dictionary<string, object?>
            {
                ["source"] = listSource,
                ["allowedCapabilities"] = Array.Empty<string>(),
                ["maxSteps"] = 200L,
                ["maxOutputCharacters"] = 100
            }).ConfigureAwait(false);
        var listRun = StructuredRoot(listRunResult.StructuredContent);
        Assert(listRun.GetProperty("success").GetBoolean(), "Expected MCP run to accept the repaired list source.");
        Assert(listRun.GetProperty("returnValue").GetProperty("value").GetInt64() == 10,
            "Expected MCP bounded list fold value 10.");
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
