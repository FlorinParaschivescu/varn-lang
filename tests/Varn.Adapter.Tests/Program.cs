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

    private const string OrderCalculationProgram = """
        budget[steps=300]
        rec Order(items:list[i64],customerTier:str)
        rec Settlement(total:i64,discount:i64)
        fn total(a:list[i64])->i64
            var b:i64 0
            each c:i64 in a max 16
                set b b + c
            end
            ret b
        end
        fn rate(a:str)->i64
            if a == "gold"
                ret 10
            end
            ret 0
        end
        fn main(a:Order)->Settlement
            let b:i64 total(a.items)
            let c:i64 rate(a.customerTier)
            ret rec[Settlement](total=b,discount=b * c / 100)
        end
        """;

    private const string RuleWithFailureProgram = """
        budget[steps=300]
        rec Order(items:list[i64],customerTier:str)
        rec Settlement(total:i64,discount:i64)
        fn rate(a:str)->result[i64]
            if a == "gold"
                ret ok(10)
            end
            ret err[i64](str.concat("unknown tier: ",a))
        end
        fn main(a:Order)->result[Settlement]
            var b:i64 0
            each c:i64 in a.items max 16
                set b b + c
            end
            if ok d:i64 rate(a.customerTier)
                if ok e:i64 num.div(b * d,100)
                    ret ok(rec[Settlement](total=b,discount=e))
                else err f:str
                    ret err[Settlement](f)
                end
            else err g:str
                ret err[Settlement](g)
            end
            ret err[Settlement]("unreachable")
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
            ("adapter reports the declared input contract", CheckReportsInputContract),
            ("adapter binds structured host input", RunBindsStructuredInput),
            ("adapter rejects input that violates the contract", RunRejectsInvalidInput),
            ("adapter separates a failed rule from a rejected run", RunSeparatesFailureFromRejection),
            ("MCP stdio host supports optional, list, and record check-inspect-run", McpHostSupportsCheckRepairRun)
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

    private static Task CheckReportsInputContract()
    {
        var response = new VarnToolService().Check(OrderCalculationProgram);
        Assert(response.Success, FormatDiagnostics(response.Diagnostics));
        var contract = response.Contract ?? throw new InvalidOperationException("Expected a declared contract.");
        var input = contract.Input ?? throw new InvalidOperationException("Expected a declared input contract.");
        Assert(input.Type == "Order", "Expected the Order input contract.");
        Assert(
            input.Fields.Select(static field => $"{field.Name}:{field.Type}")
                .SequenceEqual(["items:list[i64]", "customerTier:str"]),
            "Expected the declared input fields in declaration order.");
        Assert(contract.Result == "Settlement", "Expected the declared result type.");

        var withoutInput = new VarnToolService().Check(HelloProgram);
        Assert(withoutInput.Contract?.Input is null, "Expected no input contract for a program without one.");
        Assert(withoutInput.Contract?.Result == "i64", "Expected an i64 result contract.");
        return Task.CompletedTask;
    }

    private static async Task RunBindsStructuredInput()
    {
        (string Input, long Total, long Discount)[] cases =
        [
            ("""{"items":[1200,850,300],"customerTier":"gold"}""", 2350, 235),
            ("""{"items":[100,40],"customerTier":"basic"}""", 140, 0)
        ];

        foreach (var (input, total, discount) in cases)
        {
            var response = await new VarnToolService()
                .RunAsync(OrderCalculationProgram, [], 300, 100, input)
                .ConfigureAwait(false);
            Assert(response.Success, FormatDiagnostics(response.Diagnostics));
            Assert(response.ExitCode == 0, "Expected a structured result to exit 0.");
            var returnValue = response.ReturnValue
                ?? throw new InvalidOperationException("Expected a structured return value.");
            Assert(returnValue.Type == "Settlement", "Expected the declared result record.");
            var json = JsonSerializer.SerializeToElement(returnValue.Value);
            Assert(
                json[0].GetProperty("name").GetString() == "total" &&
                json[0].GetProperty("value").GetProperty("value").GetInt64() == total,
                $"Expected total {total}.");
            Assert(
                json[1].GetProperty("name").GetString() == "discount" &&
                json[1].GetProperty("value").GetProperty("value").GetInt64() == discount,
                $"Expected discount {discount}.");
        }
    }

    private static async Task RunRejectsInvalidInput()
    {
        var missingField = await new VarnToolService()
            .RunAsync(OrderCalculationProgram, [], 300, 100, """{"items":[1]}""")
            .ConfigureAwait(false);
        Assert(!missingField.Success, "Expected an incomplete input to fail.");
        Assert(missingField.Steps == 0, "Expected input validation to precede execution.");
        AssertHasDiagnostic(missingField.Diagnostics, "VARN6007");

        var missingInput = await new VarnToolService()
            .RunAsync(OrderCalculationProgram, [], 300, 100)
            .ConfigureAwait(false);
        AssertHasDiagnostic(missingInput.Diagnostics, "VARN6000");

        var unexpectedInput = await new VarnToolService()
            .RunAsync(HelloProgram, ["console.write"], 100, 100, """{"a":1}""")
            .ConfigureAwait(false);
        AssertHasDiagnostic(unexpectedInput.Diagnostics, "VARN6001");
    }

    private static async Task RunSeparatesFailureFromRejection()
    {
        var failedRule = await new VarnToolService()
            .RunAsync(RuleWithFailureProgram, [], 300, 100, """{"items":[1200],"customerTier":"platinum"}""")
            .ConfigureAwait(false);
        Assert(failedRule.Success, "Expected a rule that did not hold to still be a successful run.");
        Assert(failedRule.Diagnostics.Count == 0, FormatDiagnostics(failedRule.Diagnostics));
        Assert(failedRule.ExitCode == 1, "Expected a failed rule to exit 1.");
        var failure = JsonSerializer.SerializeToElement(failedRule.ReturnValue!.Value);
        Assert(!failure.GetProperty("ok").GetBoolean(), "Expected a failed result value.");
        Assert(
            failure.GetProperty("error").GetProperty("value").GetString() == "unknown tier: platinum",
            "Expected the rule's own failure reason.");
        Assert(failure.GetProperty("value").ValueKind == JsonValueKind.Null, "Expected no success value.");

        var rejectedRun = await new VarnToolService()
            .RunAsync(RuleWithFailureProgram, [], 300, 100, """{"items":[1200]}""")
            .ConfigureAwait(false);
        Assert(!rejectedRun.Success, "Expected an invalid input to be a rejected run, not a failed rule.");
        AssertHasDiagnostic(rejectedRun.Diagnostics, "VARN6007");

        var succeeded = await new VarnToolService()
            .RunAsync(RuleWithFailureProgram, [], 300, 100, """{"items":[1200,850,300],"customerTier":"gold"}""")
            .ConfigureAwait(false);
        Assert(succeeded.Success && succeeded.ExitCode == 0, "Expected a holding rule to exit 0.");
        var success = JsonSerializer.SerializeToElement(succeeded.ReturnValue!.Value);
        Assert(success.GetProperty("ok").GetBoolean(), "Expected a successful result value.");
        Assert(
            success.GetProperty("value").GetProperty("value")[1].GetProperty("value").GetProperty("value").GetInt64() == 235,
            "Expected the 235 discount nested inside the successful result.");
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
            client.ServerInstructions?.Contains("loop step:i64 from 0 to 4 max 4", StringComparison.Ordinal) is true,
            "Expected compact Varn syntax guidance.");
        Assert(
            client.ServerInstructions?.Contains("if let value:i64 answer", StringComparison.Ordinal) is true,
            "Expected compact optional syntax guidance.");
        Assert(
            client.ServerInstructions?.Contains("list[i64](1,2,3)", StringComparison.Ordinal) is true,
            "Expected compact typed-list syntax guidance.");
        Assert(
            client.ServerInstructions?.Contains("each value:i64 in values max 3", StringComparison.Ordinal) is true,
            "Expected compact bounded list traversal guidance.");
        Assert(
            client.ServerInstructions?.Contains("rec Order(items:list[i64],tier:str)", StringComparison.Ordinal) is true,
            "Expected compact record declaration guidance.");
        Assert(
            client.ServerInstructions?.Contains("read a field with order.items", StringComparison.Ordinal) is true,
            "Expected compact record field access guidance.");
        Assert(
            client.ServerInstructions?.Contains("fn main(order:Order)->Settlement", StringComparison.Ordinal) is true,
            "Expected host input contract guidance.");
        Assert(
            client.ServerInstructions?.Contains("Never write the data into the source", StringComparison.Ordinal) is true,
            "Expected guidance against embedding data in the source.");
        Assert(
            client.ServerInstructions?.Contains("Every callable operation:", StringComparison.Ordinal) is true,
            "Expected an exhaustive standard library listing.");
        Assert(
            client.ServerInstructions?.Contains("if ok value:i64", StringComparison.Ordinal) is true,
            "Expected result inspection guidance.");
        Assert(
            client.ServerInstructions?.Contains("use num.div when the divisor is data", StringComparison.Ordinal) is true,
            "Expected guidance on checked division.");
        Assert(
            client.ServerInstructions?.Contains("Arithmetic and comparison are infix", StringComparison.Ordinal) is true &&
            client.ServerInstructions?.Contains("do not invent a function", StringComparison.Ordinal) is true,
            "Expected guidance on infix operators and a closed function set.");

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
            fn maybe(a:bool)->i64?
                if a
                    ret some(42)
                end
                ret none[i64]
            end
            fn main()->i64
                let a:i64? maybe(true)
                if let b:i64 a
                    ret b
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
            inspect.GetProperty("canonical").GetString()?.Contains("J(b:i64,V[a])", StringComparison.Ordinal) is true,
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
                let a:list[i64] list[i64](1,2,3,4)
                each b:bool in a max 4
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
                let a:list[i64] list[i64](1,2,3,4)
                var b:i64 0
                each c:i64 in a max 4
                    set b b + c
                end
                let d:i64? list.get(a,9)
                if let e:i64 d
                    ret -1
                else
                    ret b
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
        Assert(canonical?.Contains("H(c:i64,V[a],4)", StringComparison.Ordinal) is true,
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

        const string invalidRecordSource = """
            budget[steps=300]
            rec Order(items:list[i64],tier:str)
            fn main()->i64
                let a:Order rec[Order](items=list[i64](1200,850,300))
                ret list.length(a.lines)
            end
            """;
        var invalidRecordResult = await client.CallToolAsync(
            "varn_check",
            new Dictionary<string, object?> { ["source"] = invalidRecordSource }).ConfigureAwait(false);
        var invalidRecord = StructuredRoot(invalidRecordResult.StructuredContent);
        var recordDiagnostics = invalidRecord.GetProperty("diagnostics").EnumerateArray()
            .Select(static diagnostic => diagnostic.GetProperty("code").GetString())
            .ToArray();
        Assert(
            recordDiagnostics.Contains("VARN3039"),
            "Expected MCP to report the missing record field.");
        Assert(
            recordDiagnostics.Contains("VARN3044"),
            "Expected MCP to report the undeclared record field.");

        const string recordSource = """
            budget[steps=300]
            rec Order(items:list[i64],tier:str)
            rec Settlement(total:i64,discount:i64)
            fn total(a:list[i64])->i64
                var b:i64 0
                each c:i64 in a max 8
                    set b b + c
                end
                ret b
            end
            fn settle(a:Order)->Settlement
                let b:i64 total(a.items)
                if a.tier == "gold"
                    ret rec[Settlement](total=b,discount=b / 10)
                end
                ret rec[Settlement](discount=0,total=b)
            end
            fn main()->i64
                let a:Order rec[Order](items=list[i64](1200,850,300),tier="gold")
                ret settle(a).discount
            end
            """;
        var recordCheckResult = await client.CallToolAsync(
            "varn_check",
            new Dictionary<string, object?> { ["source"] = recordSource }).ConfigureAwait(false);
        var recordCheck = StructuredRoot(recordCheckResult.StructuredContent);
        Assert(recordCheck.GetProperty("success").GetBoolean(), "Expected MCP check to accept the repaired record source.");

        var recordInspectResult = await client.CallToolAsync(
            "varn_inspect",
            new Dictionary<string, object?> { ["source"] = recordSource }).ConfigureAwait(false);
        var recordCanonical = StructuredRoot(recordInspectResult.StructuredContent)
            .GetProperty("canonical").GetString();
        Assert(
            recordCanonical?.Contains("T[Order(items:list[i64];tier:str);Settlement(total:i64;discount:i64)]", StringComparison.Ordinal) is true,
            "Expected MCP canonical inspection to include ordered record declarations.");
        Assert(
            recordCanonical?.Contains("W[Settlement](total=K[i64:0]", StringComparison.Ordinal) is false,
            "Expected canonical record construction to normalize to declared field order.");
        Assert(
            recordCanonical?.Contains("W[Settlement](total=V[b];discount=K[i64:0])", StringComparison.Ordinal) is true,
            "Expected canonical record construction in declared field order.");
        Assert(
            recordCanonical?.Contains("G[discount](A[settle(V[a])])", StringComparison.Ordinal) is true,
            "Expected canonical field access on a call result.");

        var recordRunResult = await client.CallToolAsync(
            "varn_run",
            new Dictionary<string, object?>
            {
                ["source"] = recordSource,
                ["allowedCapabilities"] = Array.Empty<string>(),
                ["maxSteps"] = 300L,
                ["maxOutputCharacters"] = 100
            }).ConfigureAwait(false);
        var recordRun = StructuredRoot(recordRunResult.StructuredContent);
        Assert(recordRun.GetProperty("success").GetBoolean(), "Expected MCP run to accept the repaired record source.");
        Assert(recordRun.GetProperty("returnValue").GetProperty("value").GetInt64() == 235,
            "Expected MCP structured order calculation to return a 235 discount.");

        var contractCheckResult = await client.CallToolAsync(
            "varn_check",
            new Dictionary<string, object?> { ["source"] = OrderCalculationProgram }).ConfigureAwait(false);
        var contract = StructuredRoot(contractCheckResult.StructuredContent).GetProperty("contract");
        Assert(contract.GetProperty("result").GetString() == "Settlement", "Expected the MCP result contract.");
        var contractFields = contract.GetProperty("input").GetProperty("fields").EnumerateArray()
            .Select(static field => $"{field.GetProperty("name").GetString()}:{field.GetProperty("type").GetString()}")
            .ToArray();
        Assert(
            contract.GetProperty("input").GetProperty("type").GetString() == "Order" &&
            contractFields.SequenceEqual(["items:list[i64]", "customerTier:str"]),
            "Expected MCP check to report the declared input contract.");

        (string Input, long Discount)[] inputs =
        [
            ("""{"items":[1200,850,300],"customerTier":"gold"}""", 235),
            ("""{"items":[100,40],"customerTier":"basic"}""", 0),
            ("""{"items":[],"customerTier":"gold"}""", 0)
        ];

        foreach (var (input, discount) in inputs)
        {
            var inputRunResult = await client.CallToolAsync(
                "varn_run",
                new Dictionary<string, object?>
                {
                    ["source"] = OrderCalculationProgram,
                    ["allowedCapabilities"] = Array.Empty<string>(),
                    ["maxSteps"] = 300L,
                    ["maxOutputCharacters"] = 100,
                    ["input"] = input
                }).ConfigureAwait(false);
            var inputRun = StructuredRoot(inputRunResult.StructuredContent);
            Assert(inputRun.GetProperty("success").GetBoolean(), $"Expected MCP run to accept input {input}.");
            var fields = inputRun.GetProperty("returnValue").GetProperty("value").EnumerateArray().ToArray();
            Assert(
                fields[1].GetProperty("name").GetString() == "discount" &&
                fields[1].GetProperty("value").GetProperty("value").GetInt64() == discount,
                $"Expected discount {discount} for input {input}.");
        }

        var badInputResult = await client.CallToolAsync(
            "varn_run",
            new Dictionary<string, object?>
            {
                ["source"] = OrderCalculationProgram,
                ["allowedCapabilities"] = Array.Empty<string>(),
                ["maxSteps"] = 300L,
                ["maxOutputCharacters"] = 100,
                ["input"] = """{"items":[1200,"850"],"customerTier":"gold"}"""
            }).ConfigureAwait(false);
        var badInput = StructuredRoot(badInputResult.StructuredContent);
        Assert(!badInput.GetProperty("success").GetBoolean(), "Expected MCP run to reject a mistyped input element.");
        Assert(badInput.GetProperty("steps").GetInt64() == 0, "Expected MCP input validation to precede execution.");
        Assert(
            badInput.GetProperty("diagnostics").EnumerateArray()
                .Any(static diagnostic => diagnostic.GetProperty("message").GetString()?
                    .Contains("items[1]", StringComparison.Ordinal) is true),
            "Expected MCP to name the failing input element path.");
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
