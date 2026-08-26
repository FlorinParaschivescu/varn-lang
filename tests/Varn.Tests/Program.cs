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
            ("runtime executes the first milestone", RuntimeExecutesMilestone),
            ("runtime requires a host capability grant", RuntimeRequiresHostGrant),
            ("runtime enforces the step budget", RuntimeEnforcesBudget),
            ("custom modules can be injected", CustomModuleCanBeInjected),
            ("canonical inspection is deterministic", CanonicalInspectionIsDeterministic)
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
        var result = VarnLexer.Lex("fn main()->i64\nret 0\nend\n");
        Assert(result.Diagnostics.Count == 0, "Expected no lexer diagnostics.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Arrow), "Expected an arrow token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Integer && token.Text == "0"), "Expected the i64 literal.");
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
        var check = CreateEngine().Check(HelloProgram);
        Assert(check.IsValid, FormatDiagnostics(check.Diagnostics));
        var first = CanonicalFormatter.Format(check.Program);
        var second = CanonicalFormatter.Format(check.Program);
        Assert(first == second, "Canonical formatter changed output for the same tree.");
        Assert(first.StartsWith("P{C[console.write];B[100];F[", StringComparison.Ordinal), "Unexpected canonical prefix.");
        return Task.CompletedTask;
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
