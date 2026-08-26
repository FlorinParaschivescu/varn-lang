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
            ("optional type and value contracts are explicit", OptionalTypeAndValueContractsAreExplicit),
            ("optionals branch over present values", OptionalsBranchOverPresentValues),
            ("optionals branch over absent values", OptionalsBranchOverAbsentValues),
            ("modules can produce optional values", ModulesCanProduceOptionalValues),
            ("checker requires an optional if-let source", CheckerRequiresOptionalIfLetSource),
            ("checker matches the if-let binding type", CheckerMatchesIfLetBindingType),
            ("optional bindings do not escape", OptionalBindingDoesNotEscape),
            ("optional bindings are immutable", OptionalBindingIsImmutable),
            ("checker rejects unsupported optional element types", CheckerRejectsUnsupportedOptionalElementTypes),
            ("checker requires exact optional construction types", CheckerRequiresExactOptionalConstructionTypes),
            ("list type and value contracts are explicit", ListTypeAndValueContractsAreExplicit),
            ("list length and safe lookup execute", ListLengthAndSafeLookupExecute),
            ("safe lookup represents out-of-range indexes as absence", SafeLookupRepresentsOutOfRangeAsAbsence),
            ("bounded each traversal folds homogeneous values", BoundedEachTraversalFoldsValues),
            ("runtime enforces the each ceiling", RuntimeEnforcesEachCeiling),
            ("checker rejects list element mismatches", CheckerRejectsListElementMismatches),
            ("checker rejects unsupported list element types", CheckerRejectsUnsupportedListElementTypes),
            ("checker validates each source, binding, and ceiling", CheckerValidatesEachContracts),
            ("each bindings do not escape", EachBindingDoesNotEscape),
            ("each bindings are immutable", EachBindingIsImmutable),
            ("checker validates list operations", CheckerValidatesListOperations),
            ("list construction charges one step per element", ListConstructionChargesPerElement),
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
        var result = VarnLexer.Lex("var @0:i64 0\nset @0 1\nlet @2:i64? some(1)\nlet @3:i64? none[i64]\nlet @4:list[i64] list[i64](1)\nif true\nloop @1:i64 from 0 to 1 max 1\nend\neach @5:i64 in @4 max 1\nend\nend\n");
        Assert(result.Diagnostics.Count == 0, "Expected no lexer diagnostics.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Var), "Expected a var token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Set), "Expected a set token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Question), "Expected an optional type marker.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Some), "Expected a some token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.None), "Expected a none token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.If), "Expected an if token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Loop), "Expected a loop token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Max), "Expected a max token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.List), "Expected a list token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Each), "Expected an each token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.In), "Expected an in token.");
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

    private static Task OptionalTypeAndValueContractsAreExplicit()
    {
        var optionalType = VarnType.Optional(VarnType.I64);
        Assert(optionalType.Name == "i64?", $"Expected i64?, got {optionalType}.");
        Assert(optionalType.IsOptional, "Expected the type to be optional.");
        Assert(optionalType.OptionalElementType == VarnType.I64, "Expected i64 as the optional element type.");
        Assert(VarnType.Parse("i64?") == optionalType, "Expected optional parsing to be canonical.");

        var some = VarnValue.Some(VarnValue.From(42L));
        Assert(some.Type == optionalType, "Expected some(42) to have type i64?.");
        Assert(some.IsSome, "Expected a present optional.");
        Assert(some.AsOptionalValue().AsI64() == 42, "Expected contained value 42.");
        Assert(some.ToCanonicalString() == "some(42)", "Expected canonical present optional.");

        var none = VarnValue.None(VarnType.I64);
        Assert(none.Type == optionalType, "Expected none[i64] to have type i64?.");
        Assert(!none.IsSome, "Expected an absent optional.");
        Assert(none.ToCanonicalString() == "none[i64]", "Expected canonical absent optional.");

        var response = VarnJsonFormatter.CreateRunResponse(new VarnRunResult(some, [], 0), string.Empty);
        var optionalResponse = response.ReturnValue
            ?? throw new InvalidOperationException("Expected a structured optional response.");
        Assert(optionalResponse.Type == "i64?", "Expected the structured optional type.");
        var nestedResponse = optionalResponse.Value as VarnValueResponse
            ?? throw new InvalidOperationException("Expected a nested structured present value.");
        Assert(nestedResponse.Type == "i64" && Convert.ToInt64(nestedResponse.Value) == 42, "Expected nested value 42.");

        AssertOptionalFactoryRejects(VarnType.Null);
        AssertOptionalFactoryRejects(VarnType.Any);
        AssertOptionalFactoryRejects(optionalType);
        return Task.CompletedTask;
    }

    private static async Task OptionalsBranchOverPresentValues()
    {
        var result = await CreateEngine().RunAsync(OptionalProgram(present: true)).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 42, "Expected the present branch to return 42.");
        Assert(result.Steps == 8, $"Expected deterministic step count 8, got {result.Steps}.");
    }

    private static async Task OptionalsBranchOverAbsentValues()
    {
        var result = await CreateEngine().RunAsync(OptionalProgram(present: false)).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 7, "Expected the absent branch to return 7.");
        Assert(result.Steps == 8, $"Expected deterministic step count 8, got {result.Steps}.");
    }

    private static async Task ModulesCanProduceOptionalValues()
    {
        const string source = """
            budget[steps=50]
            fn main()->i64
                let @0:i64? test.maybe(false)
                if let @1:i64 @0
                    ret @1
                else
                    ret 7
                end
                ret 0
            end
            """;
        var engine = CreateEngine();
        engine.AddModule(new TestModule());
        var result = await engine.RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 7, "Expected the module-produced absence to select the else branch.");
    }

    private static Task CheckerRequiresOptionalIfLetSource()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:i64 1
                if let @1:i64 @0
                    ret @1
                end
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3026");
        return Task.CompletedTask;
    }

    private static Task CheckerMatchesIfLetBindingType()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:i64? some(1)
                if let @1:str @0
                    ret 1
                end
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3027");
        return Task.CompletedTask;
    }

    private static Task OptionalBindingDoesNotEscape()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:i64? some(1)
                if let @1:i64 @0
                end
                ret @1
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3010");
        return Task.CompletedTask;
    }

    private static Task OptionalBindingIsImmutable()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:i64? some(1)
                if let @1:i64 @0
                    set @1 2
                end
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3024");
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsUnsupportedOptionalElementTypes()
    {
        string[] sources =
        [
            """
            budget[steps=20]
            fn main()->i64
                let @0:null? none[null]
                ret 0
            end
            """,
            """
            budget[steps=20]
            fn main()->i64
                let @0:i64?? none[i64?]
                ret 0
            end
            """,
            """
            budget[steps=20]
            fn main()->i64
                let @0:i64? some(null)
                ret 0
            end
            """
        ];

        foreach (var source in sources)
        {
            AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3028");
        }

        return Task.CompletedTask;
    }

    private static Task CheckerRequiresExactOptionalConstructionTypes()
    {
        const string someMismatch = """
            budget[steps=20]
            fn main()->i64
                let @0:i64? some(true)
                ret 0
            end
            """;
        const string noneMismatch = """
            budget[steps=20]
            fn main()->i64
                let @0:i64? none[str]
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(someMismatch).Diagnostics, "VARN3006");
        AssertHasDiagnostic(CreateEngine().Check(noneMismatch).Diagnostics, "VARN3006");
        return Task.CompletedTask;
    }

    private static string OptionalProgram(bool present) => $$"""
        budget[steps=100]
        fn maybe(@0:bool)->i64?
            if @0
                ret some(42)
            end
            ret none[i64]
        end
        fn main()->i64
            let @0:i64? maybe({{present.ToString().ToLowerInvariant()}})
            if let @1:i64 @0
                ret @1
            else
                ret 7
            end
            ret 0
        end
        """;

    private static Task ListTypeAndValueContractsAreExplicit()
    {
        var type = VarnType.List(VarnType.I64);
        Assert(type.IsList, "Expected a list type.");
        Assert(type.ListElementType == VarnType.I64, "Expected the i64 list element type.");
        Assert(VarnType.Parse("list[i64]") == type, "Expected list type parsing to be stable.");

        var value = VarnValue.FromList(VarnType.I64, [VarnValue.From(1L), VarnValue.From(2L)]);
        Assert(value.Type == type, "Expected list[i64].");
        Assert(value.AsList().Select(static item => item.AsI64()).SequenceEqual([1L, 2L]), "Expected immutable list values.");
        Assert(value.ToCanonicalString() == "list[i64](1,2)", "Expected canonical SDK list text.");
        AssertListFactoryRejects(VarnType.I64, [VarnValue.From(true)]);
        AssertListFactoryRejects(VarnType.Null, []);
        AssertListFactoryRejects(VarnType.I64, Enumerable.Repeat(VarnValue.From(0L), VarnValue.MaxListElements + 1));
        AssertOptionalFactoryRejects(type);
        return Task.CompletedTask;
    }

    private static async Task ListLengthAndSafeLookupExecute()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                let @0:list[i64] list[i64](10,20,30)
                let @1:i64 list.length(@0)
                let @2:i64? list.get(@0,1)
                if let @3:i64 @2
                    ret add(@1,@3)
                end
                ret 0
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 23, "Expected length 3 plus value 20.");
    }

    private static async Task SafeLookupRepresentsOutOfRangeAsAbsence()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                let @0:list[i64] list[i64](10,20)
                let @1:i64? list.get(@0,-1)
                let @2:i64? list.get(@0,2)
                if let @3:i64 @1
                    ret @3
                end
                if let @4:i64 @2
                    ret @4
                end
                ret 7
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 7, "Expected both invalid indexes to be absent.");
    }

    private static async Task BoundedEachTraversalFoldsValues()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                let @0:list[i64] list[i64](1,2,3,4)
                var @1:i64 0
                each @2:i64 in @0 max 4
                    set @1 add(@1,@2)
                end
                ret @1
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 10, "Expected the bounded fold to return 10.");
    }

    private static async Task RuntimeEnforcesEachCeiling()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                let @0:list[i64] list[i64](1,2,3)
                each @1:i64 in @0 max 2
                end
                ret 0
            end
            """;
        var check = CreateEngine().Check(source);
        Assert(check.IsValid, FormatDiagnostics(check.Diagnostics));
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        AssertHasDiagnostic(result.Diagnostics, "VARN4006");
    }

    private static Task CheckerRejectsListElementMismatches()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:list[i64] list[i64](1,true)
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3030");
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsUnsupportedListElementTypes()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:list[null] list[null]()
                let @1:list[list[i64]] list[list[i64]]()
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        Assert(diagnostics.Count(diagnostic => diagnostic.Code == "VARN3029") >= 2, FormatDiagnostics(diagnostics));

        var elements = Enumerable.Repeat("0", VarnValue.MaxListElements + 1);
        var oversized = $"budget[steps=20]\nfn main()->i64\nlet @0:list[i64] list[i64]({string.Join(',', elements)})\nret 0\nend\n";
        AssertHasDiagnostic(CreateEngine().Check(oversized).Diagnostics, "VARN3031");
        return Task.CompletedTask;
    }

    private static Task CheckerValidatesEachContracts()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                each @0:i64 in 1 max 1
                end
                let @1:list[i64] list[i64](1)
                each @2:bool in @1 max 1
                end
                each @3:i64 in @1 max 1025
                end
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        AssertHasDiagnostic(diagnostics, "VARN3032");
        AssertHasDiagnostic(diagnostics, "VARN3033");
        AssertHasDiagnostic(diagnostics, "VARN3034");
        return Task.CompletedTask;
    }

    private static Task EachBindingDoesNotEscape()
    {
        const string source = """
            budget[steps=30]
            fn main()->i64
                let @0:list[i64] list[i64](1)
                each @1:i64 in @0 max 1
                end
                ret @1
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3010");
        return Task.CompletedTask;
    }

    private static Task EachBindingIsImmutable()
    {
        const string source = """
            budget[steps=30]
            fn main()->i64
                let @0:list[i64] list[i64](1)
                each @1:i64 in @0 max 1
                    set @1 2
                end
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3024");
        return Task.CompletedTask;
    }

    private static Task CheckerValidatesListOperations()
    {
        const string source = """
            budget[steps=30]
            fn main()->i64
                let @0:i64 list.length(1)
                let @1:list[i64] list[i64](1)
                let @2:i64? list.get(@1,true)
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        AssertHasDiagnostic(diagnostics, "VARN3035");
        AssertHasDiagnostic(diagnostics, "VARN3015");
        return Task.CompletedTask;
    }

    private static async Task ListConstructionChargesPerElement()
    {
        const string empty = """
            budget[steps=100]
            fn main()->i64
                let @0:list[i64] list[i64]()
                ret list.length(@0)
            end
            """;
        const string populated = """
            budget[steps=100]
            fn main()->i64
                let @0:list[i64] list[i64](1,2,3)
                ret list.length(@0)
            end
            """;
        var emptyResult = await CreateEngine().RunAsync(empty).ConfigureAwait(false);
        var populatedResult = await CreateEngine().RunAsync(populated).ConfigureAwait(false);
        Assert(emptyResult.IsSuccess && populatedResult.IsSuccess, "Expected both list programs to execute.");
        Assert(populatedResult.Steps - emptyResult.Steps == 3, "Expected one deterministic construction step per element.");
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
                let @2:i64? some(1)
                let @3:i64? none[i64]
                if let @4:i64 @2
                    set @1 add(@1,@4)
                end
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
        Assert(first.Contains("L(@2:i64?,P(K[i64:1]))", StringComparison.Ordinal), "Canonical output omitted the present optional.");
        Assert(first.Contains("L(@3:i64?,N[i64])", StringComparison.Ordinal), "Canonical output omitted the absent optional.");
        Assert(first.Contains("J(@4:i64,V[@2])", StringComparison.Ordinal), "Canonical output omitted the if-let binding.");
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

    private static void AssertListFactoryRejects(VarnType elementType, IEnumerable<VarnValue> values)
    {
        try
        {
            _ = VarnValue.FromList(elementType, values);
            throw new InvalidOperationException($"Expected list factory to reject {elementType} values.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void AssertOptionalFactoryRejects(VarnType elementType)
    {
        try
        {
            _ = VarnValue.None(elementType);
            throw new InvalidOperationException($"Expected optional factory to reject {elementType}.");
        }
        catch (ArgumentException)
        {
        }
    }

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
            builder.Function(
                new VarnFunctionSignature("test.maybe", [VarnType.Bool], VarnType.Optional(VarnType.I64)),
                static (_, arguments, _) => ValueTask.FromResult(
                    arguments[0].AsBool() ? VarnValue.Some(VarnValue.From(42L)) : VarnValue.None(VarnType.I64)));
        }
    }
}
