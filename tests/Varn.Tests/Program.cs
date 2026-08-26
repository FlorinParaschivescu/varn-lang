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
            ("record type and value contracts are explicit", RecordTypeAndValueContractsAreExplicit),
            ("records construct and read typed fields", RecordsConstructAndReadTypedFields),
            ("record construction normalizes declared field order", RecordConstructionNormalizesFieldOrder),
            ("records print with deterministic field order", RecordsPrintWithDeterministicFieldOrder),
            ("checker reports exact record construction faults", CheckerReportsRecordConstructionFaults),
            ("checker rejects duplicate and reserved record declarations", CheckerRejectsDuplicateAndReservedRecords),
            ("checker rejects duplicate and unsupported record fields", CheckerRejectsDuplicateAndUnsupportedRecordFields),
            ("checker rejects unknown record types", CheckerRejectsUnknownRecordTypes),
            ("checker validates field access", CheckerValidatesFieldAccess),
            ("records are immutable and have no dynamic access", RecordsAreImmutable),
            ("records are valid optional and list element types", RecordsAreValidElementTypes),
            ("checker rejects recursive records", CheckerRejectsRecursiveRecords),
            ("nested records survive host input and output", NestedRecordsRoundTrip),
            ("lists of records fold over structured elements", ListsOfRecordsFold),
            ("list.append builds a list without mutation", ListAppendBuildsList),
            ("list.append enforces the element ceiling", ListAppendEnforcesCeiling),
            ("values can be formatted into failure messages", ValuesFormatIntoMessages),
            ("a function needs no unreachable trailing ret", NoUnreachableTrailingRet),
            ("record construction charges one step per field", RecordConstructionChargesPerField),
            ("modules can produce record values", ModulesCanProduceRecordValues),
            ("JSON record values keep declared field order", JsonRecordValuesKeepFieldOrder),
            ("entry point accepts a record input and structured result", EntryPointAcceptsRecordInputAndResult),
            ("one checked program runs over several host inputs", OneProgramRunsOverSeveralInputs),
            ("checker validates the entry point contract", CheckerValidatesEntryPointContract),
            ("program input contract is derivable without source", ProgramInputContractIsDerivable),
            ("input binding enforces the declared contract", InputBindingEnforcesDeclaredContract),
            ("input binding rejects malformed documents", InputBindingRejectsMalformedDocuments),
            ("input binding reports exact field faults", InputBindingReportsFieldFaults),
            ("input binding requires exact value types", InputBindingRequiresExactValueTypes),
            ("input binds optionals, booleans, and floats", InputBindsOptionalsBooleansAndFloats),
            ("input binding precedes execution", InputBindingPrecedesExecution),
            ("boolean operations combine conditions", BooleanOperationsCombineConditions),
            ("boolean operations evaluate both operands", BooleanOperationsEvaluateBothOperands),
            ("comparison set is complete over ordered types", ComparisonSetIsComplete),
            ("f64 comparison follows IEEE NaN semantics", F64ComparisonFollowsIeee),
            ("arithmetic covers mod, abs, min, and max", ArithmeticCoversModAbsMinMax),
            ("string operations are ordinal", StringOperationsAreOrdinal),
            ("list containment charges one step per element", ListContainmentChargesPerElement),
            ("standard library rejects inexact operand types", StandardLibraryRejectsInexactTypes),
            ("a compound rule needs no helper function", CompoundRuleNeedsNoHelperFunction),
            ("loop keywords are usable as ordinary names", ContextualKeywordsAreUsableAsNames),
            ("result type and value contracts are explicit", ResultTypeAndValueContractsAreExplicit),
            ("an expected failure is a value, not a diagnostic", ExpectedFailureIsAValue),
            ("if ok binds the success and failure sides", IfOkBindsBothSides),
            ("checker validates result inspection", CheckerValidatesResultInspection),
            ("checker requires a str failure value", CheckerRequiresStrFailure),
            ("checker rejects unsupported result value types", CheckerRejectsUnsupportedResultValueTypes),
            ("result bindings do not escape and are immutable", ResultBindingsDoNotEscape),
            ("checked division reports failure instead of trapping", CheckedDivisionReportsFailure),
            ("conversion and parsing return results", ConversionAndParsingReturnResults),
            ("entry point may return a result", EntryPointMayReturnResult),
            ("canonical inspection includes results", CanonicalInspectionIncludesResults),
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
        var result = VarnLexer.Lex("var @0:i64 0\nset @0 1\nlet @2:i64? some(1)\nlet @3:i64? none[i64]\nlet @4:list[i64] list[i64](1)\nrec Pair(a:i64)\nlet @6:Pair rec[Pair](a=1)\nlet @7:i64 @6.a\nif true\nloop @1:i64 from 0 to 1 max 1\nend\neach @5:i64 in @4 max 1\nend\nend\n");
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
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Rec), "Expected a rec token.");
        Assert(result.Tokens.Any(static token => token.Kind == TokenKind.Dot), "Expected a field access token.");
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

    private static Task RecordTypeAndValueContractsAreExplicit()
    {
        var shape = new VarnRecordShape(
            "Order",
            [new VarnRecordField("items", VarnType.List(VarnType.I64)), new VarnRecordField("tier", VarnType.String)]);
        Assert(shape.Type == VarnType.Parse("Order"), "Expected a named record type.");
        Assert(shape.IndexOf("items") == 0 && shape.IndexOf("tier") == 1, "Expected declared field order.");
        Assert(shape.IndexOf("absent") == -1, "Expected an undeclared field to be absent.");

        var value = VarnValue.FromRecord(
            shape,
            [VarnValue.FromList(VarnType.I64, [VarnValue.From(1L)]), VarnValue.From("gold")]);
        Assert(value.Type == shape.Type, "Expected the record type.");
        Assert(value.IsRecord, "Expected a record value.");
        Assert(value.AsRecord().GetField("tier").Value as string == "gold", "Expected lookup by field name.");
        Assert(
            value.ToCanonicalString() == "Order(items=list[i64](1),tier=gold)",
            $"Expected canonical SDK record text, got '{value.ToCanonicalString()}'.");

        AssertRecordShapeRejects([new VarnRecordField("a", VarnType.I64), new VarnRecordField("a", VarnType.Bool)]);
        AssertRecordFactoryRejects(shape, [VarnValue.From("gold")]);
        AssertRecordFactoryRejects(shape, [VarnValue.From("gold"), VarnValue.From("gold")]);
        AssertRecordFactoryRejects(new VarnRecordShape("Bad", [new VarnRecordField("a", VarnType.Null)]), [VarnValue.Null]);
        return Task.CompletedTask;
    }

    private static async Task RecordsConstructAndReadTypedFields()
    {
        var result = await CreateEngine().RunAsync(OrderProgram).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 235, "Expected a 10 percent discount on a 2350 total.");
    }

    private static async Task RecordConstructionNormalizesFieldOrder()
    {
        const string declared = """
            budget[steps=100]
            rec Pair(a:i64,b:i64)
            fn main()->i64
                let @0:Pair rec[Pair](a=1,b=2)
                ret sub(@0.a,@0.b)
            end
            """;
        const string reordered = """
            budget[steps=100]
            rec Pair(a:i64,b:i64)
            fn main()->i64
                let @0:Pair rec[Pair](b=2,a=1)
                ret sub(@0.a,@0.b)
            end
            """;
        var declaredCheck = CreateEngine().Check(declared);
        var reorderedCheck = CreateEngine().Check(reordered);
        Assert(declaredCheck.IsValid && reorderedCheck.IsValid, FormatDiagnostics(reorderedCheck.Diagnostics));
        var canonical = CanonicalFormatter.Format(declaredCheck.Program);
        Assert(
            canonical == CanonicalFormatter.Format(reorderedCheck.Program),
            "Expected source field order to normalize to declared field order.");
        Assert(
            canonical.Contains("T[Pair(a:i64;b:i64)]", StringComparison.Ordinal),
            "Canonical output omitted the record declaration.");
        Assert(
            canonical.Contains("W[Pair](a=K[i64:1];b=K[i64:2])", StringComparison.Ordinal),
            "Canonical output omitted normalized record construction.");
        Assert(
            canonical.Contains("G[a](V[@0])", StringComparison.Ordinal),
            "Canonical output omitted field access.");

        var declaredResult = await CreateEngine().RunAsync(declared).ConfigureAwait(false);
        var reorderedResult = await CreateEngine().RunAsync(reordered).ConfigureAwait(false);
        Assert(declaredResult.ReturnValue?.AsI64() == -1, "Expected a-b to be -1.");
        Assert(
            declaredResult.ReturnValue?.AsI64() == reorderedResult.ReturnValue?.AsI64() &&
            declaredResult.Steps == reorderedResult.Steps,
            "Expected identical results and step accounting regardless of source field order.");
    }

    private static async Task RecordsPrintWithDeterministicFieldOrder()
    {
        const string source = """
            cap[console.write]
            budget[steps=100]
            rec Pair(a:i64,b:str)
            fn main()->i64 ![console]
                io.print(rec[Pair](b="x",a=1))
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
        Assert(output.ToString().Trim() == "Pair(a=1,b=x)", $"Expected declared field order, got '{output}'.");
    }

    private static Task CheckerReportsRecordConstructionFaults()
    {
        const string source = """
            budget[steps=40]
            rec Order(items:list[i64],tier:str)
            fn main()->i64
                let @0:Order rec[Order](items=list[i64](1),tier="g",extra=1)
                let @1:Order rec[Order](items=list[i64](1))
                let @2:Order rec[Order](items=list[i64](1),tier="g",tier="h")
                let @3:Order rec[Order](items=1,tier="g")
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        AssertHasDiagnostic(diagnostics, "VARN3039");
        AssertHasDiagnostic(diagnostics, "VARN3040");
        AssertHasDiagnostic(diagnostics, "VARN3041");
        AssertHasDiagnostic(diagnostics, "VARN3042");
        Assert(
            diagnostics.Single(static diagnostic => diagnostic.Code == "VARN3039").Message
                .Contains("tier", StringComparison.Ordinal),
            "Expected the missing field to be named.");
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsDuplicateAndReservedRecords()
    {
        const string source = """
            budget[steps=20]
            rec i64(a:i64)
            rec Pair(a:i64)
            rec Pair(b:i64)
            fn main()->i64
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        Assert(
            diagnostics.Count(static diagnostic => diagnostic.Code == "VARN3036") == 2,
            FormatDiagnostics(diagnostics));
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsDuplicateAndUnsupportedRecordFields()
    {
        const string source = """
            budget[steps=20]
            rec Duplicated(a:i64,a:str)
            rec Unsupported(a:null,b:any,c:list[list[i64]],d:i64??,e:Undeclared)
            fn main()->i64
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        AssertHasDiagnostic(diagnostics, "VARN3037");
        Assert(
            diagnostics.Count(static diagnostic => diagnostic.Code == "VARN3038") == 5,
            FormatDiagnostics(diagnostics));

        const string nested = """
            budget[steps=20]
            rec Addr(city:str)
            rec Person(name:str,home:Addr,past:list[Addr],work:Addr?)
            fn main()->i64
                ret 0
            end
            """;
        var nestedCheck = CreateEngine().Check(nested);
        Assert(nestedCheck.IsValid, FormatDiagnostics(nestedCheck.Diagnostics));
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsRecursiveRecords()
    {
        const string direct = """
            budget[steps=20]
            rec Node(next:Node)
            fn main()->i64
                ret 0
            end
            """;
        const string mutual = """
            budget[steps=20]
            rec A(b:list[B])
            rec B(a:A)
            fn main()->i64
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(direct).Diagnostics, "VARN3049");
        var mutualDiagnostics = CreateEngine().Check(mutual).Diagnostics;
        Assert(
            mutualDiagnostics.Count(static diagnostic => diagnostic.Code == "VARN3049") == 2,
            FormatDiagnostics(mutualDiagnostics));
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsUnknownRecordTypes()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:Missing rec[Missing](a=1)
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        Assert(
            diagnostics.Count(static diagnostic => diagnostic.Code == "VARN3017") == 2,
            FormatDiagnostics(diagnostics));
        return Task.CompletedTask;
    }

    private static Task CheckerValidatesFieldAccess()
    {
        const string source = """
            budget[steps=40]
            rec Order(items:list[i64],tier:str)
            fn main()->i64
                let @0:i64 1
                let @1:str @0.tier
                let @2:Order rec[Order](items=list[i64](1),tier="g")
                let @3:str @2.absent
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        AssertHasDiagnostic(diagnostics, "VARN3043");
        AssertHasDiagnostic(diagnostics, "VARN3044");
        return Task.CompletedTask;
    }

    private static Task RecordsAreImmutable()
    {
        const string fieldAssignment = """
            budget[steps=40]
            rec Pair(a:i64,b:i64)
            fn main()->i64
                let @0:Pair rec[Pair](a=1,b=2)
                set @0.a 3
                ret 0
            end
            """;
        const string slotAssignment = """
            budget[steps=40]
            rec Pair(a:i64,b:i64)
            fn main()->i64
                let @0:Pair rec[Pair](a=1,b=2)
                set @0 rec[Pair](a=3,b=4)
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(fieldAssignment).Diagnostics, "VARN2004");
        AssertHasDiagnostic(CreateEngine().Check(slotAssignment).Diagnostics, "VARN3024");
        return Task.CompletedTask;
    }

    private static Task RecordsAreValidElementTypes()
    {
        const string allowed = """
            budget[steps=20]
            rec Pair(a:i64)
            fn main()->i64
                let @0:Pair? none[Pair]
                let @1:list[Pair] list[Pair]()
                ret 0
            end
            """;
        var check = CreateEngine().Check(allowed);
        Assert(check.IsValid, FormatDiagnostics(check.Diagnostics));

        const string tooDeep = """
            budget[steps=20]
            rec Pair(a:i64)
            fn main()->i64
                let @0:Pair?? none[Pair?]
                let @1:list[list[Pair]] list[list[Pair]]()
                let @2:Missing? none[Missing]
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(tooDeep).Diagnostics;
        AssertHasDiagnostic(diagnostics, "VARN3028");
        AssertHasDiagnostic(diagnostics, "VARN3029");
        return Task.CompletedTask;
    }

    private static async Task RecordConstructionChargesPerField()
    {
        const string one = """
            budget[steps=100]
            rec One(a:i64)
            fn main()->i64
                let @0:One rec[One](a=1)
                ret @0.a
            end
            """;
        const string two = """
            budget[steps=100]
            rec Two(a:i64,b:i64)
            fn main()->i64
                let @0:Two rec[Two](a=1,b=2)
                ret @0.a
            end
            """;
        var oneResult = await CreateEngine().RunAsync(one).ConfigureAwait(false);
        var twoResult = await CreateEngine().RunAsync(two).ConfigureAwait(false);
        Assert(oneResult.IsSuccess && twoResult.IsSuccess, FormatDiagnostics(twoResult.Diagnostics));
        Assert(twoResult.Steps - oneResult.Steps == 1, "Expected one deterministic construction step per field.");
    }

    private static async Task ModulesCanProduceRecordValues()
    {
        const string source = """
            budget[steps=40]
            rec Point(x:i64,y:i64)
            fn main()->i64
                let @0:Point test.point(40)
                ret add(@0.x,@0.y)
            end
            """;
        var engine = CreateEngine();
        engine.AddModule(new TestModule());
        var result = await engine.RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 42, "Expected the module-produced record fields to sum to 42.");
    }

    private static Task JsonRecordValuesKeepFieldOrder()
    {
        var shape = new VarnRecordShape(
            "Settlement",
            [new VarnRecordField("total", VarnType.I64), new VarnRecordField("discount", VarnType.I64)]);
        var value = VarnValue.FromRecord(shape, [VarnValue.From(2350L), VarnValue.From(235L)]);
        var json = VarnJsonFormatter.FormatRun(new VarnRunResult(value, [], 1), string.Empty);
        using var document = JsonDocument.Parse(json);
        var returnValue = document.RootElement.GetProperty("returnValue");
        Assert(returnValue.GetProperty("type").GetString() == "Settlement", "Expected the record type name.");
        var fields = returnValue.GetProperty("value").EnumerateArray().ToArray();
        Assert(fields.Length == 2, "Expected two record fields.");
        Assert(fields[0].GetProperty("name").GetString() == "total", "Expected the declared first field.");
        Assert(fields[0].GetProperty("value").GetProperty("value").GetInt64() == 2350, "Expected the total value.");
        Assert(fields[1].GetProperty("name").GetString() == "discount", "Expected the declared second field.");
        Assert(fields[1].GetProperty("value").GetProperty("type").GetString() == "i64", "Expected a typed field value.");
        return Task.CompletedTask;
    }

    private const string OrderProgram = """
        budget[steps=300]
        rec Order(items:list[i64],tier:str)
        rec Settlement(total:i64,discount:i64)
        fn total(@0:list[i64])->i64
            var @1:i64 0
            each @2:i64 in @0 max 8
                set @1 add(@1,@2)
            end
            ret @1
        end
        fn discount(@0:i64,@1:str)->i64
            if eq(@1,"gold")
                ret div(@0,10)
            end
            ret 0
        end
        fn settle(@0:Order)->Settlement
            let @1:i64 total(@0.items)
            let @2:i64 discount(@1,@0.tier)
            ret rec[Settlement](total=@1,discount=@2)
        end
        fn main()->i64
            let @0:Order rec[Order](items=list[i64](1200,850,300),tier="gold")
            let @1:Settlement settle(@0)
            ret @1.discount
        end
        """;

    private static async Task EntryPointAcceptsRecordInputAndResult()
    {
        var result = await RunWithInputAsync(
            OrderCalculationProgram,
            """{"items":[1200,850,300],"customerTier":"gold"}""").ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        var settlement = result.ReturnValue?.AsRecord()
            ?? throw new InvalidOperationException("Expected a structured result.");
        Assert(settlement.Shape.Name == "Settlement", "Expected the declared result record.");
        Assert(settlement.GetField("total").AsI64() == 2350, "Expected a 2350 total.");
        Assert(settlement.GetField("discount").AsI64() == 235, "Expected a 235 discount.");
        Assert(result.ExitCode == 0, "Expected a structured result to exit 0.");
    }

    private static async Task OneProgramRunsOverSeveralInputs()
    {
        (string Input, long Total, long Discount)[] cases =
        [
            ("""{"items":[1200,850,300],"customerTier":"gold"}""", 2350, 235),
            ("""{"items":[100],"customerTier":"basic"}""", 100, 0),
            ("""{"items":[],"customerTier":"gold"}""", 0, 0)
        ];

        foreach (var (input, total, discount) in cases)
        {
            var result = await RunWithInputAsync(OrderCalculationProgram, input).ConfigureAwait(false);
            Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
            var settlement = result.ReturnValue!.Value.AsRecord();
            Assert(settlement.GetField("total").AsI64() == total, $"Expected total {total} for {input}.");
            Assert(settlement.GetField("discount").AsI64() == discount, $"Expected discount {discount} for {input}.");
        }

        var first = await RunWithInputAsync(OrderCalculationProgram, cases[0].Input).ConfigureAwait(false);
        var repeat = await RunWithInputAsync(OrderCalculationProgram, cases[0].Input).ConfigureAwait(false);
        Assert(first.Steps == repeat.Steps, "Expected the same input to consume the same steps.");
    }

    private static Task CheckerValidatesEntryPointContract()
    {
        string[] sources =
        [
            """
            budget[steps=20]
            rec Pair(a:i64)
            fn main(@0:Pair,@1:Pair)->i64
                ret 0
            end
            """,
            """
            budget[steps=20]
            fn main(@0:i64)->i64
                ret 0
            end
            """,
            """
            budget[steps=20]
            fn main()->str
                ret "x"
            end
            """
        ];

        foreach (var source in sources)
        {
            AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3004");
        }

        Assert(CreateEngine().Check(OrderCalculationProgram).IsValid, "Expected a record entry point to be valid.");
        return Task.CompletedTask;
    }

    private static Task ProgramInputContractIsDerivable()
    {
        var check = CreateEngine().Check(OrderCalculationProgram);
        Assert(check.IsValid, FormatDiagnostics(check.Diagnostics));
        var input = VarnProgramContract.InputShape(check.Program)
            ?? throw new InvalidOperationException("Expected a declared input contract.");
        Assert(input.Name == "Order", "Expected the Order input contract.");
        Assert(
            input.Fields.Select(static field => $"{field.Name}:{field.Type}")
                .SequenceEqual(["items:list[i64]", "customerTier:str"]),
            "Expected the declared input fields in declaration order.");
        Assert(
            VarnProgramContract.ResultType(check.Program) == VarnType.Parse("Settlement"),
            "Expected the declared result type.");
        Assert(
            VarnProgramContract.InputShape(CreateEngine().Check(HelloProgram).Program) is null,
            "Expected a program without input to declare no contract.");
        return Task.CompletedTask;
    }

    private static async Task InputBindingEnforcesDeclaredContract()
    {
        var missing = await RunWithInputAsync(OrderCalculationProgram, null).ConfigureAwait(false);
        AssertHasDiagnostic(missing.Diagnostics, "VARN6000");

        var unexpected = await RunWithInputAsync(
            """
            budget[steps=20]
            fn main()->i64
                ret 0
            end
            """,
            """{"items":[]}""").ConfigureAwait(false);
        AssertHasDiagnostic(unexpected.Diagnostics, "VARN6001");
    }

    private static async Task InputBindingRejectsMalformedDocuments()
    {
        var oversized = await RunWithInputAsync(
            OrderCalculationProgram,
            new string('x', VarnInputBinder.MaximumInputCharacters + 1)).ConfigureAwait(false);
        AssertHasDiagnostic(oversized.Diagnostics, "VARN6002");

        var malformed = await RunWithInputAsync(OrderCalculationProgram, "{\"items\":").ConfigureAwait(false);
        AssertHasDiagnostic(malformed.Diagnostics, "VARN6003");

        var notAnObject = await RunWithInputAsync(OrderCalculationProgram, "[1,2,3]").ConfigureAwait(false);
        AssertHasDiagnostic(notAnObject.Diagnostics, "VARN6004");
    }

    private static async Task InputBindingReportsFieldFaults()
    {
        var unknown = await RunWithInputAsync(
            OrderCalculationProgram,
            """{"items":[1],"customerTier":"gold","tier":"gold"}""").ConfigureAwait(false);
        AssertHasDiagnostic(unknown.Diagnostics, "VARN6005");

        var duplicated = await RunWithInputAsync(
            OrderCalculationProgram,
            """{"items":[1],"customerTier":"gold","customerTier":"basic"}""").ConfigureAwait(false);
        AssertHasDiagnostic(duplicated.Diagnostics, "VARN6006");

        var incomplete = await RunWithInputAsync(
            OrderCalculationProgram,
            """{"items":[1]}""").ConfigureAwait(false);
        AssertHasDiagnostic(incomplete.Diagnostics, "VARN6007");
        Assert(
            incomplete.Diagnostics.Single(static diagnostic => diagnostic.Code == "VARN6007").Message
                .Contains("customerTier", StringComparison.Ordinal),
            "Expected the missing input field to be named.");
    }

    private static async Task InputBindingRequiresExactValueTypes()
    {
        var mismatched = await RunWithInputAsync(
            OrderCalculationProgram,
            """{"items":"1200","customerTier":"gold"}""").ConfigureAwait(false);
        AssertHasDiagnostic(mismatched.Diagnostics, "VARN6008");

        var elementMismatch = await RunWithInputAsync(
            OrderCalculationProgram,
            """{"items":[1200,true],"customerTier":"gold"}""").ConfigureAwait(false);
        AssertHasDiagnostic(elementMismatch.Diagnostics, "VARN6008");
        Assert(
            elementMismatch.Diagnostics.Single(static diagnostic => diagnostic.Code == "VARN6008").Message
                .Contains("items[1]", StringComparison.Ordinal),
            "Expected the failing element path to be named.");

        foreach (var value in new[] { "1200.5", "99999999999999999999" })
        {
            var notAnInteger = await RunWithInputAsync(
                OrderCalculationProgram,
                $$"""{"items":[{{value}}],"customerTier":"gold"}""").ConfigureAwait(false);
            AssertHasDiagnostic(notAnInteger.Diagnostics, "VARN6009");
        }

        var elements = string.Join(',', Enumerable.Repeat("1", VarnValue.MaxListElements + 1));
        var oversized = await RunWithInputAsync(
            OrderCalculationProgram,
            $$"""{"items":[{{elements}}],"customerTier":"gold"}""").ConfigureAwait(false);
        AssertHasDiagnostic(oversized.Diagnostics, "VARN6010");
    }

    private static async Task InputBindsOptionalsBooleansAndFloats()
    {
        const string source = """
            budget[steps=100]
            rec Profile(name:str,age:i64?,active:bool,score:f64)
            fn main(@0:Profile)->i64
                if @0.active
                    if let @1:i64 @0.age
                        ret @1
                    end
                end
                ret -1
            end
            """;
        var present = await RunWithInputAsync(
            source,
            """{"name":"ada","age":36,"active":true,"score":1.5}""").ConfigureAwait(false);
        Assert(present.IsSuccess, FormatDiagnostics(present.Diagnostics));
        Assert(present.ReturnValue?.AsI64() == 36, "Expected a present optional to bind.");

        var absent = await RunWithInputAsync(
            source,
            """{"name":"ada","age":null,"active":true,"score":1.5}""").ConfigureAwait(false);
        Assert(absent.IsSuccess, FormatDiagnostics(absent.Diagnostics));
        Assert(absent.ReturnValue?.AsI64() == -1, "Expected JSON null to bind as a typed absence.");

        var inactive = await RunWithInputAsync(
            source,
            """{"name":"ada","age":36,"active":false,"score":1.5}""").ConfigureAwait(false);
        Assert(inactive.ReturnValue?.AsI64() == -1, "Expected the boolean field to bind.");
    }

    private static async Task InputBindingPrecedesExecution()
    {
        var rejected = await RunWithInputAsync(
            OrderCalculationProgram,
            """{"items":[1]}""").ConfigureAwait(false);
        Assert(!rejected.IsSuccess, "Expected invalid input to fail.");
        Assert(rejected.Steps == 0, "Expected no steps to be consumed before input validation succeeds.");
        Assert(rejected.ExitCode == 1, "Expected a rejected input to exit 1.");
    }

    private static ValueTask<VarnRunResult> RunWithInputAsync(string source, string? input) =>
        CreateEngine().RunAsync(source, new VarnRunOptions { Input = input });

    private const string OrderCalculationProgram = """
        budget[steps=300]
        rec Order(items:list[i64],customerTier:str)
        rec Settlement(total:i64,discount:i64)
        fn total(@0:list[i64])->i64
            var @1:i64 0
            each @2:i64 in @0 max 16
                set @1 add(@1,@2)
            end
            ret @1
        end
        fn rate(@0:str)->i64
            if eq(@0,"gold")
                ret 10
            end
            ret 0
        end
        fn main(@0:Order)->Settlement
            let @1:i64 total(@0.items)
            let @2:i64 rate(@0.customerTier)
            ret rec[Settlement](total=@1,discount=div(mul(@1,@2),100))
        end
        """;

    private static async Task BooleanOperationsCombineConditions()
    {
        (string Expression, long Expected)[] cases =
        [
            ("and(true,true)", 1),
            ("and(true,false)", 0),
            ("or(false,true)", 1),
            ("or(false,false)", 0),
            ("not(false)", 1),
            ("and(or(false,true),not(false))", 1)
        ];

        foreach (var (expression, expected) in cases)
        {
            var result = await EvaluateBoolAsync(expression).ConfigureAwait(false);
            Assert(result == expected, $"Expected {expression} to select {expected}.");
        }
    }

    private static async Task BooleanOperationsEvaluateBothOperands()
    {
        const string shortCircuitable = """
            budget[steps=100]
            fn main()->i64
                let @0:bool and(false,eq(1,1))
                let @1:bool and(true,eq(1,1))
                ret 0
            end
            """;
        const string baseline = """
            budget[steps=100]
            fn main()->i64
                let @0:bool and(false,true)
                let @1:bool and(true,true)
                ret 0
            end
            """;
        var withCalls = await CreateEngine().RunAsync(shortCircuitable).ConfigureAwait(false);
        var withLiterals = await CreateEngine().RunAsync(baseline).ConfigureAwait(false);
        Assert(withCalls.IsSuccess && withLiterals.IsSuccess, FormatDiagnostics(withCalls.Diagnostics));
        Assert(
            withCalls.Steps - withLiterals.Steps == 2,
            "Expected both operands to be evaluated regardless of the first, charging one step per nested call.");
    }

    private static async Task ComparisonSetIsComplete()
    {
        (string Expression, long Expected)[] cases =
        [
            ("lt(1,2)", 1), ("lt(2,1)", 0),
            ("gt(2,1)", 1), ("gt(1,2)", 0),
            ("lte(2,2)", 1), ("lte(3,2)", 0),
            ("gte(2,2)", 1), ("gte(1,2)", 0),
            ("ne(1,2)", 1), ("ne(2,2)", 0),
            ("eq(2,2)", 1),
            ("""lt("a","b")""", 1),
            ("""gt("b","a")""", 1),
            ("""gte("a","a")""", 1),
            ("""ne("a","b")""", 1),
            ("ne(true,false)", 1),
            ("lt(1.5,2.5)", 1),
            ("gte(2.5,2.5)", 1)
        ];

        foreach (var (expression, expected) in cases)
        {
            var result = await EvaluateBoolAsync(expression).ConfigureAwait(false);
            Assert(result == expected, $"Expected {expression} to select {expected}.");
        }
    }

    private static async Task F64ComparisonFollowsIeee()
    {
        const string source = """
            budget[steps=200]
            fn main()->i64
                let @0:f64 div(0.0,0.0)
                var @1:i64 0
                if eq(@0,@0)
                    set @1 add(@1,1)
                end
                if lt(@0,1.0)
                    set @1 add(@1,2)
                end
                if gte(@0,@0)
                    set @1 add(@1,4)
                end
                if ne(@0,@0)
                    set @1 add(@1,8)
                end
                ret @1
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(
            result.ReturnValue?.AsI64() == 8,
            "Expected NaN to compare false for eq, lt, and gte, and true only for ne.");
    }

    private static async Task ArithmeticCoversModAbsMinMax()
    {
        (string Expression, long Expected)[] cases =
        [
            ("mod(7,3)", 1),
            ("mod(-7,3)", -1),
            ("abs(-5)", 5),
            ("min(3,9)", 3),
            ("max(3,9)", 9)
        ];

        foreach (var (expression, expected) in cases)
        {
            var result = await EvaluateI64Async(expression).ConfigureAwait(false);
            Assert(result == expected, $"Expected {expression} to be {expected}, got {result}.");
        }

        var floating = await CreateEngine().RunAsync("""
            budget[steps=50]
            fn main()->i64
                let @0:f64 abs(-2.5)
                let @1:f64 max(@0,1.0)
                if eq(@1,2.5)
                    ret 1
                end
                ret 0
            end
            """).ConfigureAwait(false);
        Assert(floating.ReturnValue?.AsI64() == 1, "Expected f64 abs and max to work.");
    }

    private static async Task StringOperationsAreOrdinal()
    {
        (string Expression, long Expected)[] cases =
        [
            ("""str.contains("gold-tier","gold")""", 1),
            ("""str.contains("gold-tier","GOLD")""", 0),
            ("""str.starts_with("gold-tier","gold")""", 1),
            ("""str.ends_with("gold-tier","tier")""", 1),
            ("""str.ends_with("gold-tier","gold")""", 0),
            ("""eq(str.concat("gold","-tier"),"gold-tier")""", 1),
            ("""eq(str.length("gold"),4)""", 1),
            ("""eq(str.to_lower("GoLd"),"gold")""", 1),
            ("""eq(str.to_upper("gold"),"GOLD")""", 1),
            ("""eq(str.to_lower("gold"),"GOLD")""", 0)
        ];

        foreach (var (expression, expected) in cases)
        {
            var result = await EvaluateBoolAsync(expression).ConfigureAwait(false);
            Assert(result == expected, $"Expected {expression} to select {expected}.");
        }
    }

    private static async Task ListContainmentChargesPerElement()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                let @0:list[str] list[str]("gold","silver")
                if list.contains(@0,"silver")
                    ret 1
                end
                ret 0
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 1, "Expected list containment to find the element.");

        const string longer = """
            budget[steps=100]
            fn main()->i64
                let @0:list[str] list[str]("gold","silver","bronze")
                if list.contains(@0,"silver")
                    ret 1
                end
                ret 0
            end
            """;
        var longerResult = await CreateEngine().RunAsync(longer).ConfigureAwait(false);
        Assert(
            longerResult.Steps - result.Steps == 2,
            "Expected one construction step and one scan step for the extra element.");
    }

    private static Task StandardLibraryRejectsInexactTypes()
    {
        string[] sources =
        [
            """
            budget[steps=20]
            fn main()->i64
                let @0:bool and(true,1)
                ret 0
            end
            """,
            """
            budget[steps=20]
            fn main()->i64
                let @0:bool gt(1,1.0)
                ret 0
            end
            """,
            """
            budget[steps=20]
            fn main()->i64
                let @0:i64 str.length(1)
                ret 0
            end
            """,
            """
            budget[steps=20]
            fn main()->i64
                let @0:list[i64] list[i64](1)
                let @1:bool list.contains(@0,"1")
                ret 0
            end
            """
        ];

        foreach (var source in sources)
        {
            AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3012");
        }

        return Task.CompletedTask;
    }

    private static async Task CompoundRuleNeedsNoHelperFunction()
    {
        const string source = """
            budget[steps=300]
            rec Order(items:list[i64],customerTier:str)
            fn main(@0:Order)->i64
                var @1:i64 0
                each @2:i64 in @0.items max 16
                    set @1 add(@1,@2)
                end
                if and(gte(@1,1000),or(eq(@0.customerTier,"gold"),str.starts_with(@0.customerTier,"vip")))
                    ret div(mul(@1,10),100)
                end
                ret 0
            end
            """;
        var check = CreateEngine().Check(source);
        Assert(check.IsValid, FormatDiagnostics(check.Diagnostics));

        (string Input, long Expected)[] cases =
        [
            ("""{"items":[1200,850,300],"customerTier":"gold"}""", 235),
            ("""{"items":[1200,850,300],"customerTier":"vip-plus"}""", 235),
            ("""{"items":[1200,850,300],"customerTier":"basic"}""", 0),
            ("""{"items":[100],"customerTier":"gold"}""", 0)
        ];

        foreach (var (input, expected) in cases)
        {
            var result = await RunWithInputAsync(source, input).ConfigureAwait(false);
            Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
            Assert(result.ReturnValue?.AsI64() == expected, $"Expected {expected} for {input}.");
        }
    }

    private static async Task ContextualKeywordsAreUsableAsNames()
    {
        const string source = """
            budget[steps=200]
            rec Window(max:i64,from:i64)
            fn main()->i64
                let @0:Window rec[Window](max=9,from=3)
                let @1:list[i64] list[i64](1,2,3)
                var @2:i64 0
                each @3:i64 in @1 max 3
                    set @2 add(@2,@3)
                end
                ret max(@2,min(@0.max,@0.from))
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(
            result.ReturnValue?.AsI64() == 6,
            "Expected 'max' and 'from' to work as record fields and calls while 'each ... max' still parses.");
    }

    private static async Task<long> EvaluateBoolAsync(string expression)
    {
        var result = await CreateEngine().RunAsync($$"""
            budget[steps=200]
            fn main()->i64
                if {{expression}}
                    ret 1
                end
                ret 0
            end
            """).ConfigureAwait(false);
        Assert(result.IsSuccess, $"{expression}: {FormatDiagnostics(result.Diagnostics)}");
        return result.ReturnValue!.Value.AsI64();
    }

    private static async Task<long> EvaluateI64Async(string expression)
    {
        var result = await CreateEngine().RunAsync($$"""
            budget[steps=200]
            fn main()->i64
                ret {{expression}}
            end
            """).ConfigureAwait(false);
        Assert(result.IsSuccess, $"{expression}: {FormatDiagnostics(result.Diagnostics)}");
        return result.ReturnValue!.Value.AsI64();
    }

    private static Task ResultTypeAndValueContractsAreExplicit()
    {
        var type = VarnType.Result(VarnType.I64);
        Assert(type.IsResult, "Expected a result type.");
        Assert(type.ResultValueType == VarnType.I64, "Expected the i64 result value type.");
        Assert(VarnType.Parse("result[i64]") == type, "Expected result type parsing to be stable.");
        Assert(!type.IsList && !type.IsOptional, "Expected a result to be its own type constructor.");

        var ok = VarnValue.Ok(VarnValue.From(42L));
        Assert(ok.Type == type && ok.IsResult && ok.IsOk, "Expected a successful result[i64].");
        Assert(ok.AsResult().Value.AsI64() == 42, "Expected the carried success value.");
        Assert(ok.ToCanonicalString() == "ok(42)", $"Expected canonical ok text, got '{ok.ToCanonicalString()}'.");

        var err = VarnValue.Err(VarnType.I64, "divide by zero");
        Assert(err.Type == type && err.IsResult && !err.IsOk, "Expected a failed result[i64].");
        Assert(err.AsResult().Value.Value as string == "divide by zero", "Expected the carried failure message.");
        Assert(
            err.ToCanonicalString() == "err[i64](divide by zero)",
            $"Expected canonical err text, got '{err.ToCanonicalString()}'.");

        AssertResultFactoryRejects(VarnType.Null);
        AssertResultFactoryRejects(VarnType.Any);
        AssertResultFactoryRejects(VarnType.List(VarnType.I64));
        AssertResultFactoryRejects(VarnType.Optional(VarnType.I64));
        AssertResultFactoryRejects(VarnType.Result(VarnType.I64));
        AssertOptionalFactoryRejects(type);
        AssertListFactoryRejects(type, []);
        return Task.CompletedTask;
    }

    private static async Task ExpectedFailureIsAValue()
    {
        var failed = await RunWithInputAsync(
            RuleWithFailureProgram,
            """{"items":[1200,850,300],"customerTier":"platinum"}""").ConfigureAwait(false);
        Assert(failed.IsSuccess, "Expected an in-domain failure to still be a successful run.");
        Assert(failed.Diagnostics.Count == 0, FormatDiagnostics(failed.Diagnostics));
        var value = failed.ReturnValue ?? throw new InvalidOperationException("Expected a result value.");
        Assert(value.IsResult && !value.IsOk, "Expected a failed result value.");
        Assert(
            value.AsResult().Value.Value as string == "unknown tier: platinum",
            "Expected the rule's own failure message.");
        Assert(failed.ExitCode == 1, "Expected a failed rule to exit 1 even though the run succeeded.");

        var succeeded = await RunWithInputAsync(
            RuleWithFailureProgram,
            """{"items":[1200,850,300],"customerTier":"gold"}""").ConfigureAwait(false);
        Assert(succeeded.ReturnValue?.IsOk == true, "Expected a successful result.");
        Assert(
            succeeded.ReturnValue!.Value.AsResult().Value.AsRecord().GetField("discount").AsI64() == 235,
            "Expected the 235 discount inside the successful result.");
        Assert(succeeded.ExitCode == 0, "Expected a successful structured result to exit 0.");
    }

    private static async Task IfOkBindsBothSides()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                if ok @0:i64 num.div(10,0)
                    ret @0
                else err @1:str
                    ret str.length(@1)
                end
                ret -1
            end
            """;
        var result = await CreateEngine().RunAsync(source).ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsI64() == 14, "Expected the bound failure message 'divide by zero'.");

        const string withoutErrorBinding = """
            budget[steps=100]
            fn main()->i64
                if ok @0:i64 num.div(10,2)
                    ret @0
                else
                    ret -1
                end
                ret 0
            end
            """;
        var plain = await CreateEngine().RunAsync(withoutErrorBinding).ConfigureAwait(false);
        Assert(plain.ReturnValue?.AsI64() == 5, "Expected the success side without an error binding.");
    }

    private static Task CheckerValidatesResultInspection()
    {
        const string notAResult = """
            budget[steps=20]
            fn main()->i64
                let @0:i64 1
                if ok @1:i64 @0
                    ret @1
                end
                ret 0
            end
            """;
        const string wrongBinding = """
            budget[steps=20]
            fn main()->i64
                if ok @0:str num.div(4,2)
                    ret 1
                end
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(notAResult).Diagnostics, "VARN3047");
        AssertHasDiagnostic(CreateEngine().Check(wrongBinding).Diagnostics, "VARN3048");
        return Task.CompletedTask;
    }

    private static Task CheckerRequiresStrFailure()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:result[i64] err[i64](42)
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(source).Diagnostics, "VARN3046");
        return Task.CompletedTask;
    }

    private static Task CheckerRejectsUnsupportedResultValueTypes()
    {
        const string source = """
            budget[steps=20]
            fn main()->i64
                let @0:result[null] err[null]("x")
                let @1:result[list[i64]] err[list[i64]]("x")
                let @2:result[result[i64]] err[result[i64]]("x")
                let @3:result[Missing] err[Missing]("x")
                ret 0
            end
            """;
        var diagnostics = CreateEngine().Check(source).Diagnostics;
        Assert(
            diagnostics.Count(static diagnostic => diagnostic.Code == "VARN3045") >= 4,
            FormatDiagnostics(diagnostics));
        return Task.CompletedTask;
    }

    private static Task ResultBindingsDoNotEscape()
    {
        const string escapes = """
            budget[steps=40]
            fn main()->i64
                if ok @0:i64 num.div(4,2)
                end
                ret @0
            end
            """;
        const string mutates = """
            budget[steps=40]
            fn main()->i64
                if ok @0:i64 num.div(4,2)
                    set @0 9
                end
                ret 0
            end
            """;
        const string errorEscapes = """
            budget[steps=40]
            fn main()->i64
                if ok @0:i64 num.div(4,2)
                    ret @0
                else err @1:str
                    ret 0
                end
                ret str.length(@1)
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(escapes).Diagnostics, "VARN3010");
        AssertHasDiagnostic(CreateEngine().Check(mutates).Diagnostics, "VARN3024");
        AssertHasDiagnostic(CreateEngine().Check(errorEscapes).Diagnostics, "VARN3010");
        return Task.CompletedTask;
    }

    private static async Task CheckedDivisionReportsFailure()
    {
        (string Expression, string Expected)[] cases =
        [
            ("num.div(10,0)", "divide by zero"),
            ("num.mod(10,0)", "divide by zero")
        ];

        foreach (var (expression, expected) in cases)
        {
            var message = await EvaluateFailureAsync(expression).ConfigureAwait(false);
            Assert(message == expected, $"Expected {expression} to fail with '{expected}', got '{message}'.");
        }

        var trapped = await CreateEngine().RunAsync("""
            budget[steps=40]
            fn main()->i64
                ret div(10,0)
            end
            """).ConfigureAwait(false);
        Assert(!trapped.IsSuccess, "Expected total div by zero to remain a trap.");
        AssertHasDiagnostic(trapped.Diagnostics, "VARN4003");
    }

    private static async Task ConversionAndParsingReturnResults()
    {
        (string Expression, string? Expected)[] failures =
        [
            ("""str.to_i64("nope")""", "not an i64"),
            ("""str.to_f64("nope")""", "not an f64"),
            ("num.to_i64(1.5)", "not a whole number"),
            ("num.to_i64(div(0.0,0.0))", "not a finite number")
        ];

        foreach (var (expression, expected) in failures)
        {
            var message = await EvaluateFailureAsync(expression).ConfigureAwait(false);
            Assert(message == expected, $"Expected {expression} to fail with '{expected}', got '{message}'.");
        }

        var parsed = await CreateEngine().RunAsync("""
            budget[steps=100]
            fn main()->i64
                if ok @0:i64 str.to_i64("41")
                    ret add(@0,1)
                end
                ret -1
            end
            """).ConfigureAwait(false);
        Assert(parsed.IsSuccess, FormatDiagnostics(parsed.Diagnostics));
        Assert(parsed.ReturnValue?.AsI64() == 42, "Expected a parsed 41 plus one.");

        var widened = await CreateEngine().RunAsync("""
            budget[steps=100]
            fn main()->i64
                let @0:f64 num.to_f64(3)
                if gt(@0,2.5)
                    ret 1
                end
                ret 0
            end
            """).ConfigureAwait(false);
        Assert(widened.ReturnValue?.AsI64() == 1, "Expected total i64 to f64 widening.");
    }

    private static Task EntryPointMayReturnResult()
    {
        Assert(CreateEngine().Check(RuleWithFailureProgram).IsValid, "Expected a result entry point to be valid.");
        Assert(
            CreateEngine().Check("""
                budget[steps=20]
                fn main()->result[i64]
                    ret ok(0)
                end
                """).IsValid,
            "Expected result[i64] to be a valid entry point type.");
        AssertHasDiagnostic(
            CreateEngine().Check("""
                budget[steps=20]
                fn main()->result[str]
                    ret ok("x")
                end
                """).Diagnostics,
            "VARN3004");
        return Task.CompletedTask;
    }

    private static Task CanonicalInspectionIncludesResults()
    {
        const string source = """
            budget[steps=100]
            fn main()->i64
                if ok @0:i64 num.div(4,2)
                    ret @0
                else err @1:str
                    ret str.length(@1)
                end
                ret 0
            end
            """;
        var check = CreateEngine().Check(source);
        Assert(check.IsValid, FormatDiagnostics(check.Diagnostics));
        var canonical = CanonicalFormatter.Format(check.Program);
        Assert(
            canonical == CanonicalFormatter.Format(check.Program),
            "Canonical formatter changed output for the same tree.");
        Assert(
            canonical.Contains("U(@0:i64,A[num.div(", StringComparison.Ordinal),
            $"Canonical output omitted the result inspection: {canonical}");
        Assert(
            canonical.Contains("E[@1]{", StringComparison.Ordinal),
            "Canonical output omitted the bound failure slot.");

        var constructors = CreateEngine().Check("""
            budget[steps=20]
            fn main()->result[i64]
                let @0:result[i64] err[i64]("x")
                ret ok(1)
            end
            """);
        var constructorCanonical = CanonicalFormatter.Format(constructors.Program);
        Assert(
            constructorCanonical.Contains("""Z[i64](K[str:"x"])""", StringComparison.Ordinal),
            "Canonical output omitted err construction.");
        Assert(
            constructorCanonical.Contains("Y(K[i64:1])", StringComparison.Ordinal),
            "Canonical output omitted ok construction.");
        return Task.CompletedTask;
    }

    private static async Task<string> EvaluateFailureAsync(string expression)
    {
        var result = await CreateEngine().RunAsync($$"""
            budget[steps=200]
            rec Wrapper(message:str)
            fn main()->Wrapper
                if ok @0:i64 str.to_i64("0")
                    ret probe()
                end
                ret rec[Wrapper](message="unreachable")
            end
            fn probe()->Wrapper
                if ok @1:{{ValueTypeOf(expression)}} {{expression}}
                    ret rec[Wrapper](message="unexpected success")
                else err @2:str
                    ret rec[Wrapper](message=@2)
                end
                ret rec[Wrapper](message="unreachable")
            end
            """).ConfigureAwait(false);
        Assert(result.IsSuccess, $"{expression}: {FormatDiagnostics(result.Diagnostics)}");
        return (result.ReturnValue!.Value.AsRecord().GetField("message").Value as string)!;
    }

    private static string ValueTypeOf(string expression) =>
        expression.Contains("to_f64", StringComparison.Ordinal) ? "f64" : "i64";

    private static void AssertResultFactoryRejects(VarnType valueType)
    {
        try
        {
            _ = VarnValue.Err(valueType, "x");
            throw new InvalidOperationException($"Expected the result factory to reject {valueType}.");
        }
        catch (ArgumentException)
        {
        }
    }

    private const string RuleWithFailureProgram = """
        budget[steps=300]
        rec Order(items:list[i64],customerTier:str)
        rec Settlement(total:i64,discount:i64)
        fn rate(@0:str)->result[i64]
            if eq(@0,"gold")
                ret ok(10)
            end
            if eq(@0,"basic")
                ret ok(0)
            end
            ret err[i64](str.concat("unknown tier: ",@0))
        end
        fn main(@0:Order)->result[Settlement]
            var @1:i64 0
            each @2:i64 in @0.items max 16
                set @1 add(@1,@2)
            end
            if ok @3:i64 rate(@0.customerTier)
                if ok @4:i64 num.div(mul(@1,@3),100)
                    ret ok(rec[Settlement](total=@1,discount=@4))
                else err @5:str
                    ret err[Settlement](@5)
                end
            else err @6:str
                ret err[Settlement](@6)
            end
            ret err[Settlement]("unreachable")
        end
        """;

    private static async Task NestedRecordsRoundTrip()
    {
        const string source = """
            budget[steps=300]
            rec Addr(city:str,zip:str)
            rec Person(name:str,home:Addr,alias:str?)
            rec Label(text:str)
            fn main(@0:Person)->Label
                if let @1:str @0.alias
                    ret rec[Label](text=str.concat(@1,str.concat(" of ",@0.home.city)))
                end
                ret rec[Label](text=str.concat(@0.name,str.concat(" of ",@0.home.city)))
            end
            """;
        var named = await RunWithInputAsync(
            source,
            """{"name":"ada","home":{"city":"London","zip":"E1"},"alias":null}""").ConfigureAwait(false);
        Assert(named.IsSuccess, FormatDiagnostics(named.Diagnostics));
        Assert(
            named.ReturnValue?.AsRecord().GetField("text").Value as string == "ada of London",
            "Expected a nested field read through two records.");

        var aliased = await RunWithInputAsync(
            source,
            """{"name":"ada","home":{"city":"London","zip":"E1"},"alias":"byron"}""").ConfigureAwait(false);
        Assert(
            aliased.ReturnValue?.AsRecord().GetField("text").Value as string == "byron of London",
            "Expected an optional record field to bind.");

        var badNesting = await RunWithInputAsync(
            source,
            """{"name":"ada","home":{"city":"London"},"alias":null}""").ConfigureAwait(false);
        AssertHasDiagnostic(badNesting.Diagnostics, "VARN6007");
        Assert(
            badNesting.Diagnostics.Single(static diagnostic => diagnostic.Code == "VARN6007").Message
                .Contains("home.zip", StringComparison.Ordinal),
            "Expected the nested path to be named.");
    }

    private static async Task ListsOfRecordsFold()
    {
        const string source = """
            budget[steps=400]
            rec Line(sku:str,qty:i64,priceCents:i64)
            rec Cart(lines:list[Line])
            rec Total(cents:i64)
            fn main(@0:Cart)->Total
                var @1:i64 0
                each @2:Line in @0.lines max 32
                    set @1 add(@1,mul(@2.qty,@2.priceCents))
                end
                ret rec[Total](cents=@1)
            end
            """;
        var result = await RunWithInputAsync(
            source,
            """{"lines":[{"sku":"A","qty":2,"priceCents":500},{"sku":"B","qty":1,"priceCents":1250}]}""")
            .ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        Assert(result.ReturnValue?.AsRecord().GetField("cents").AsI64() == 2250, "Expected 2 x 500 plus 1250.");

        var badElement = await RunWithInputAsync(
            source,
            """{"lines":[{"sku":"A","qty":"2","priceCents":500}]}""").ConfigureAwait(false);
        AssertHasDiagnostic(badElement.Diagnostics, "VARN6008");
        Assert(
            badElement.Diagnostics.Single(static diagnostic => diagnostic.Code == "VARN6008").Message
                .Contains("lines[0].qty", StringComparison.Ordinal),
            "Expected the failing element path to be named.");
    }

    private static async Task ListAppendBuildsList()
    {
        const string source = """
            budget[steps=400]
            rec Nums(values:list[i64])
            rec Kept(values:list[i64])
            fn main(@0:Nums)->Kept
                var @1:list[i64] list[i64]()
                each @2:i64 in @0.values max 32
                    if gt(@2,10)
                        set @1 list.append(@1,@2)
                    end
                end
                ret rec[Kept](values=@1)
            end
            """;
        var result = await RunWithInputAsync(source, """{"values":[5,15,25,3,40]}""").ConfigureAwait(false);
        Assert(result.IsSuccess, FormatDiagnostics(result.Diagnostics));
        var kept = result.ReturnValue!.Value.AsRecord().GetField("values").AsList()
            .Select(static value => value.AsI64()).ToArray();
        Assert(kept.SequenceEqual([15L, 25L, 40L]), $"Expected 15,25,40, got {string.Join(",", kept)}.");

        const string mismatch = """
            budget[steps=40]
            fn main()->i64
                let @0:list[i64] list.append(list[i64](1),"x")
                ret 0
            end
            """;
        AssertHasDiagnostic(CreateEngine().Check(mismatch).Diagnostics, "VARN3015");
    }

    private static async Task ListAppendEnforcesCeiling()
    {
        var elements = string.Join(',', Enumerable.Repeat("1", VarnValue.MaxListElements));
        var source = $$"""
            budget[steps=5000]
            fn main()->i64
                let @0:list[i64] list[i64]({{elements}})
                let @1:list[i64] list.append(@0,2)
                ret list.length(@1)
            end
            """;
        var result = await CreateEngine().RunAsync(source, new VarnRunOptions { MaxSteps = 100_000 })
            .ConfigureAwait(false);
        AssertHasDiagnostic(result.Diagnostics, "VARN4007");
    }

    private static async Task ValuesFormatIntoMessages()
    {
        const string source = """
            budget[steps=200]
            rec Order(totalCents:i64,limitCents:i64)
            rec Receipt(totalCents:i64)
            fn main(@0:Order)->result[Receipt]
                if gt(@0.totalCents,@0.limitCents)
                    ret err[Receipt](str.concat("over limit of ",str.from_i64(@0.limitCents)))
                end
                ret ok(rec[Receipt](totalCents=@0.totalCents))
            end
            """;
        var rejected = await RunWithInputAsync(source, """{"totalCents":15000,"limitCents":10000}""")
            .ConfigureAwait(false);
        Assert(rejected.IsSuccess, FormatDiagnostics(rejected.Diagnostics));
        Assert(
            rejected.ReturnValue!.Value.AsResult().Value.Value as string == "over limit of 10000",
            "Expected the failing value inside the message.");

        Assert(await EvaluateBoolAsync("""eq(str.from_f64(1.5),"1.5")""").ConfigureAwait(false) == 1,
            "Expected invariant f64 formatting.");
        Assert(await EvaluateBoolAsync("""eq(str.from_bool(true),"true")""").ConfigureAwait(false) == 1,
            "Expected bool formatting.");
    }

    private static Task NoUnreachableTrailingRet()
    {
        const string everyBranchReturns = """
            budget[steps=100]
            fn main()->i64
                if true
                    ret 1
                else
                    ret 2
                end
            end
            """;
        const string canFallThrough = """
            budget[steps=100]
            fn main()->i64
                if true
                    ret 1
                end
            end
            """;
        const string loopDoesNotCount = """
            budget[steps=100]
            fn main()->i64
                loop @0:i64 from 0 to 2 max 2
                    ret 1
                end
            end
            """;
        const string emptyElseDoesNotCount = """
            budget[steps=100]
            fn main()->i64
                if true
                    ret 1
                else
                end
            end
            """;
        Assert(CreateEngine().Check(everyBranchReturns).IsValid, "Expected both-branches-return to be valid.");
        AssertHasDiagnostic(CreateEngine().Check(canFallThrough).Diagnostics, "VARN3009");
        AssertHasDiagnostic(CreateEngine().Check(loopDoesNotCount).Diagnostics, "VARN3009");
        AssertHasDiagnostic(CreateEngine().Check(emptyElseDoesNotCount).Diagnostics, "VARN3009");
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
        Assert(first.Contains(";T[];F[", StringComparison.Ordinal), "Canonical output omitted the empty record section.");
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

    private static void AssertRecordShapeRejects(IReadOnlyList<VarnRecordField> fields)
    {
        try
        {
            _ = new VarnRecordShape("Invalid", fields);
            throw new InvalidOperationException("Expected the record shape to be rejected.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static void AssertRecordFactoryRejects(VarnRecordShape shape, IEnumerable<VarnValue> values)
    {
        try
        {
            _ = VarnValue.FromRecord(shape, values);
            throw new InvalidOperationException($"Expected the record factory to reject values for {shape}.");
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
        private static readonly VarnRecordShape PointShape = new(
            "Point",
            [new VarnRecordField("x", VarnType.I64), new VarnRecordField("y", VarnType.I64)]);

        public string Name => "varn.tests";

        public void Register(VarnModuleBuilder builder)
        {
            builder.Function(
                new VarnFunctionSignature("test.point", [VarnType.I64], PointShape.Type),
                static (_, arguments, _) => ValueTask.FromResult(
                    VarnValue.FromRecord(PointShape, [arguments[0], VarnValue.From(2L)])));
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
