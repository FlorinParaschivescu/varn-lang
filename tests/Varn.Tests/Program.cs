using System.Text.Json;
using Varn.Lexer;
using Varn.ModuleSdk;
using Varn.Modules.Standard;
using Varn.Runtime;
using Varn.Syntax;

namespace Varn.Tests;

public static class Program
{
    private const string HelloProgram = """
        cap[console.write]
        budget[steps=100]

        fn sum(@0:i64,@1:i64)->i64
            let @2:i64 add(@0,@1)
            ret @2
        end

        fn main()->i64 ![console]
            let @0:i64 10
            let @1:i64 20
            let @2:i64 sum(@0,@1)
            io.print(@2)
            ret 0
        end
        """;

    public static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("lexer emits structural tokens", LexerEmitsStructuralTokens),
            ("checker accepts a typed program", CheckerAcceptsTypedProgram),
            ("checker requires declared effects", CheckerRequiresEffects),
            ("checker requires declared capabilities", CheckerRequiresCapabilities),
            ("conditions execute the selected branch", ConditionsExecuteSelectedBranch),
            ("checker requires boolean conditions", CheckerRequiresBooleanConditions),
            ("bounded loops execute half-open ranges", BoundedLoopsExecuteHalfOpenRanges),
            ("checker verifies the static loop maximum", CheckerVerifiesLoopMaximum),
            ("loop iterator scope does not leak", LoopIteratorScopeDoesNotLeak),
            ("mutable slots accumulate across bounded loops", MutableSlotsAccumulateAcrossLoops),
            ("mutable slots persist through selected branches", MutableSlotsPersistThroughSelectedBranches),
            ("checker rejects assignment to immutable slots", CheckerRejectsImmutableAssignment),
            ("checker rejects assignment to unknown slots", CheckerRejectsUnknownAssignment),
            ("checker rejects assignment outside declaration scope", CheckerRejectsOutOfScopeAssignment),
            ("checker rejects assignment with a different type", CheckerRejectsDifferentAssignmentType),
            ("checker rejects duplicate mutable slots", CheckerRejectsDuplicateMutableSlot),
            ("runtime executes the first milestone", RuntimeExecutesMilestone),
            ("runtime requires a host capability grant", RuntimeRequiresHostGrant),
            ("runtime enforces the step budget", RuntimeEnforcesBudget),
            ("custom modules can be injected", CustomModuleCanBeInjected),
            ("canonical inspection is deterministic", CanonicalInspectionIsDeterministic),
            ("JSON check responses have a stable schema", JsonCheckHasStableSchema),
            ("JSON run responses capture output and result", JsonRunCapturesOutputAndResult)
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
                Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
            }
        }

        Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
        return failures == 0 ? 0 : 1;
    }

    private static Task LexerEmitsStructuralTokens()
    {
        var result = VarnLexer.Lex("var @0:i64 0\nset @0 1\nif true\nloop @1:i64 from 0 to 1 max 1\nend\nend\n");
        Assert(result.Diagnostics.Count == 0, "Expected no lexer diagnostics.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Var), "Expected a var token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Set), "Expected a set token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.If), "Expected an if token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Loop), "Expected a loop token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Max), "Expected a max token.");
        return Task.CompletedTask;
    }

    private static Task CheckerAcceptsTypedProgram()
    {
        var result = CreateEngine().Check(HelloProgram);
        Assert(result.IsValid, FormatDiagnostics(result.Diagnostics));
        return Task.CompletedTask;
    }

    private static Task CheckerRequiresEffects()
    {
        const string source = """
            cap[console.write]
            budget[steps=20]
            fn main()->i64
                io.print(1)
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3016");
        return Task.CompletedTask;
    }

    private static Task CheckerRequiresCapabilities()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64 ![console]
                io.print(1)
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3013");
        return Task.CompletedTask;
    }

    private static async Task ConditionsExecuteSelectedBranch()
    {
        const string source = """
            budget[steps=100]

            fn choose(@0:bool)->i64
                if @0
                    ret 11
                else
                    ret 22
                end
                ret 33
            end

            fn main()->i64
                ret choose(true)
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 11, "Expected the true branch to return 11.");
    }

    private static Task CheckerRequiresBooleanConditions()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                if 1
                    ret 1
                end
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3019");
        return Task.CompletedTask;
    }

    private static async Task BoundedLoopsExecuteHalfOpenRanges()
    {
        const string source = """
            cap[console.write]
            budget[steps=100]
            fn main()->i64 ![console]
                loop @0:i64 from 0 to 3 max 3
                    io.print(@0)
                end
                ret 0
            end
            """;
        var output = new StringWriter();
        var result = await CreateEngine().RunAsync(
            source,
            new VarnRunOptions
            {
                AllowedCapabilities = new HashSet<string>(StringComparer.Ordinal) { ConsoleModule.WriteCapability },
                Output = output
            }).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert(lines.SequenceEqual(["0", "1", "2"]), $"Expected loop output 0,1,2; got '{output}'.");
    }

    private static Task CheckerVerifiesLoopMaximum()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                loop @0:i64 from 0 to 3 max 4
                end
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3023");
        return Task.CompletedTask;
    }

    private static Task LoopIteratorScopeDoesNotLeak()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                loop @0:i64 from 0 to 1 max 1
                end
                ret @0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3010");
        return Task.CompletedTask;
    }

    private static async Task MutableSlotsAccumulateAcrossLoops()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                var @0:i64 0
                loop @1:i64 from 0 to 4 max 4
                    set @0 add(@0,@1)
                end
                ret @0
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 6, "Expected the accumulator to return 6.");
        Assert(result.Steps == 16, $"Expected deterministic step count 16, got {result.Steps}.");
    }

    private static async Task MutableSlotsPersistThroughSelectedBranches()
    {
        const string source = """
            budget[steps=30]
            fn main()->i64
                var @0:i64 1
                if true
                    set @0 9
                end
                ret @0
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 9, "Expected the selected branch to update the outer mutable slot.");
    }

    private static Task CheckerRejectsImmutableAssignment()
    {
        string[] sources =
        [
            """
            budget[steps=20]
            fn main()->i64
                let @0:i64 0
                set @0 1
                ret @0
            end
            """,
            """
            budget[steps=30]
            fn update(@0:i64)->i64
                set @0 1
                ret @0
            end
            fn main()->i64
                ret update(0)
            end
            """,
            """
            budget[steps=30]
            fn main()->i64
                loop @0:i64 from 0 to 1 max 1
                    set @0 1
                end
                ret 0
            end
            """
        ];

        foreach (var source in sources)
        {
            AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3024");
        }

        return Task.CompletedTask;
    }

    private static Task CheckerRejectsUnknownAssignment()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                set @0 1
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3010");
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsOutOfScopeAssignment()
    {
        const string source = """
            budget[steps=30]
            fn main()->i64
                if true
                    var @0:i64 0
                end
                set @0 1
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3010");
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsDifferentAssignmentType()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                var @0:i64 0
                set @0 true
                ret @0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3025");
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsDuplicateMutableSlot()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                var @0:i64 0
                let @0:i64 1
                ret @0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3005");
        return Task.CompletedTask;
    }

    private static async Task RuntimeExecutesMilestone()
    {
        var output = new StringWriter();
        var result = await CreateEngine().RunAsync(
            HelloProgram,
            new VarnRunOptions
            {
                AllowedCapabilities = new HashSet<string>(StringComparer.Ordinal) { ConsoleModule.WriteCapability },
                Output = output
            }).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 0, "Expected main to return 0.");
        Assert(output.ToString().Trim() == "30", $"Expected output 30, got '{output}'.");
    }

    private static async Task RuntimeRequiresHostGrant()
    {
        var result = await CreateEngine().RunAsync(HelloProgram, new VarnRunOptions { Output = new StringWriter() })
            .ConfigureAwait(false);
        AssertHasDiagnostic(result.Diagnostics, "VARN4002");
    }

    private static async Task RuntimeEnforcesBudget()
    {
        const string source = """
            budget[steps=1]
            fn main()->i64
                ret 0
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        AssertHasDiagnostic(result.Diagnostics, "VARN4005");
    }

    private static async Task CustomModuleCanBeInjected()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:i64 test.double(21)
                ret @0
            end
            """;
        var engine = CreateEngine();
        engine.AddModule(new TestModule());
        var result = await engine.RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 42, "Expected the injected module to return 42.");
    }

    private static Task CanonicalInspectionIsDeterministic()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                if true
                    loop @0:i64 from 0 to 1 max 1
                    end
                end
                var @1:i64 0
                set @1 add(@1,1)
                ret @1
            end
            """;
        var check = CreateEngine().Check(source);
        Assert(check.IsValid, FormatDiagnostics(check.Diagnostics));
        var first = CanonicalFormatter.Format(check.Program);
        var second = CanonicalFormatter.Format(check.Program);
        Assert(first == second, "Canonical formatter changed output for the same tree.");
        Assert(first.Contains("I(", StringComparison.Ordinal), "Canonical output omitted the condition.");
        Assert(first.Contains("O(@0:i64,0,1,1)", StringComparison.Ordinal), "Canonical output omitted the loop bounds.");
        Assert(first.Contains("M(@1:i64,K[i64:0])", StringComparison.Ordinal), "Canonical output omitted the mutable declaration.");
        Assert(first.Contains("S(@1,A[add(V[@1];K[i64:1])])", StringComparison.Ordinal), "Canonical output omitted the assignment.");
        return Task.CompletedTask;
    }

    private static Task JsonCheckHasStableSchema()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                ret true
            end
            """;
        var json = VarnJsonFormatter.FormatCheck(CreateEngine().Check(source));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert(root.GetProperty("schemaVersion").GetInt32() == 1, "Expected JSON schema version 1.");
        Assert(root.GetProperty("command").GetString() == "check", "Expected a check response.");
        Assert(!root.GetProperty("success").GetBoolean(), "Expected the invalid program to fail checking.");
        var diagnostic = root.GetProperty("diagnostics").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "VARN3008");
        Assert(diagnostic.GetProperty("span").GetProperty("line").GetInt32() > 0, "Expected a source line.");
        return Task.CompletedTask;
    }

    private static async Task JsonRunCapturesOutputAndResult()
    {
        var output = new StringWriter();
        var result = await CreateEngine().RunAsync(
            HelloProgram,
            new VarnRunOptions
            {
                AllowedCapabilities = new HashSet<string>(StringComparer.Ordinal) { ConsoleModule.WriteCapability },
                Output = output
            }).ConfigureAwait(false);
        var json = VarnJsonFormatter.FormatRun(result, output.ToString());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert(root.GetProperty("schemaVersion").GetInt32() == 1, "Expected JSON schema version 1.");
        Assert(root.GetProperty("command").GetString() == "run", "Expected a run response.");
        Assert(root.GetProperty("success").GetBoolean(), "Expected a successful run response.");
        Assert(root.GetProperty("exitCode").GetInt32() == 0, "Expected exit code 0.");
        Assert(root.GetProperty("steps").GetInt64() > 0, "Expected a positive step count.");
        var returnValue = root.GetProperty("returnValue");
        Assert(returnValue.GetProperty("type").GetString() == "i64", "Expected an i64 return type.");
        Assert(returnValue.GetProperty("value").GetInt64() == 0, "Expected return value 0.");
        Assert(root.GetProperty("output").GetString()?.Trim() == "30", "Expected captured output 30.");
        Assert(root.GetProperty("diagnostics").GetArrayLength() == 0, "Expected no run diagnostics.");
    }

    private static VarnEngine CreateEngine() => new([new CoreModule(), new ConsoleModule()]);

    private static void AssertHasDiagnostic(IReadOnlyList<Diagnostic> diagnostics, string code) =>
        Assert(diagnostics.Any(diagnostic => diagnostic.Code == code), $"Expected {code}; got {FormatDiagnostics(diagnostics)}");

    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestModule : IVarnModule
    {
        public string Name => "varn.tests";

        public void Register(VarnModuleBuilder builder)
        {
            builder.Function(
                new VarnFunctionSignature("test.double", [VarnType.I64], VarnType.I64),
                static (_, arguments, _) => ValueTask.FromResult(VarnValue.From(checked(arguments[0].AsI64() * 2))));
        }
    }
}
