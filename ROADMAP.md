# Varn roadmap

## Where this is going

**Varn is the runtime you reach for when an AI writes logic that has to run in production: on the hot path, per request, without a container per execution.**

That is the defensible niche. Running AI-generated Python safely needs a microVM or container per execution, which costs 50–200 ms of cold start and real per-tenant infrastructure. Varn runs in-process, deterministic, metered, and capability-gated, in microseconds. Unlike WebAssembly, which sandboxes at a lower level, Varn's effects and capabilities are legible in the same source the AI generates and reads, so a host can decide whether to run a program without executing it or analysing it.

The realistic users are products with AI-authored business rules — discount, routing, validation, eligibility — and multi-tenant systems that must run untrusted logic inside a shared process. Varn is not trying to be a general-purpose language, and should refuse features that only make sense for one.

## Stages to real use

Each stage is defined by what somebody can actually do, not by what is architecturally next.

| | Stage | Means | Status |
| --- | --- | --- | --- |
| S1 | Verifiable core | Express and verify a structured transformation over host data. An agent generates, checks, repairs, inspects, and runs through MCP. | Done |
| S2 | Expressive enough for real rules | Standard library plus `Result`: a person can write the rule they actually came to write, and expected failures are values. | Done |
| S3 | Proven | A reproducible benchmark states where Varn beats Python-plus-JSON and where it loses. | Harness done; model-generated half open |
| S4 | Embeddable | `dotnet tool install -g varn` and NuGet packages: somebody uses Varn without cloning this repository. | Built, unpublished |
| S5 | Connected | Structured network policy and a trusted HTTP module, so rules can reach real data under explicit grants. Covers M2. | Not started |
| S6 | Multi-tenant | Isolated, signed, versioned module processes. Bytecode and a VM only if measurement demands them. Covers M4 and M5. | Not started |

M4, bytecode and the VM, is deliberately deferred. It is a performance and verification concern that matters at S6, and nothing before then needs it. Do not start it because it is interesting.

Distribution (S4) intentionally comes after expressiveness (S2) and proof (S3). Shipping an easy install before a person can write `and` converts curiosity into a bad first impression, and first impressions are spent once.

## Current focus — measure the thesis, with a model

The harness exists and the mechanism half is measured. What is missing is the half that needs a model: how *often* a real generator makes each kind of mistake, and how fast the repair loop closes.

- [x] Build a reproducible task set of small structured rules with known-correct answers.
- [x] Classify every outcome as correct, rejected before execution, crashed, or silently wrong.
- [x] Measure source size on one ruler across both languages.
- [ ] Generate solutions with a real model in both languages under identical conditions.
- [ ] Measure **silent-wrong frequency** rather than silent-wrong mechanism.
- [ ] Measure **tokens to verified-correct**, counting every repair cycle, not tokens to first output.
- [ ] Report distributions across models and temperatures, not a single run.

This needs model API access, which the harness deliberately does not assume. `bench/README.md` documents exactly where generated solutions plug in; classification, grading, and token counting already work unchanged.

Exit criterion: a reproducible benchmark states where Varn wins and where it loses, with frequencies rather than examples.

## Completed — `Result` values

- [x] Specify a closed `result[T]` type with explicit success and failure construction.
- [x] Require explicit inspection before extracting either side, in the style of `if let`.
- [x] Give in-domain division failure an explicit value rather than a trap.
- [x] Add the failable conversions the standard library was missing: numeric conversion and parsing.
- [x] Extend the checker, runtime, module SDK, JSON, and canonical format, with exhaustive tests.

Exit criterion met: `result[T]` carries a success value or a `str` reason, `ok`/`err[T]` construct it, and `if ok ... else err ...` is the only way to read either side (`VARN3045`-`VARN3048`). `main` may return `result[T]`, so a rule that does not hold reports `success` with no diagnostics and its reason in `returnValue`, exiting `1`. A rejected run still reports diagnostics. That distinction — a failed rule is not a broken run — is the point of the slice, and an adapter test pins all three outcomes over the real MCP process.

**Deviation from the plan, recorded deliberately.** This slice was scoped to "convert `div` and `mod` by zero from `VARN4003` into an explicit failure value". It instead added `num.div` and `num.mod` returning `result[i64]` and left total `div`/`mod` trapping. The reason is a distinction the original wording missed: `result` is for *expected, in-domain* failures, while a zero **literal** divisor is a defect, and making every division in every rule unwrap a result taxes the common case to handle a bug. Rust and Swift split the same way. Use `num.div` whenever the divisor is data; the trap remains for defects.

Still open: the failure side is always `str`. A structured failure type would let a host branch on error kind rather than match strings. `result[T]` can later become sugar for `result[T,str]` without breaking existing programs, so this is deferred rather than foreclosed.

## Completed — standard library

Varn exposed nine callable names and could not express `and`, which blocked both real rules and any honest benchmark.

- [x] Add total boolean operations: `and`, `or`, `not`.
- [x] Complete the comparison set: `gt`, `lte`, `gte`, `ne` over the exact types that already support `eq` and `lt`.
- [x] Add the arithmetic a rule needs: `mod`, `abs`, `min`, `max`.
- [x] Add ordinal string operations: length, concatenation, containment, and prefix/suffix tests.
- [x] Add `list.contains` for every supported element type.
- [x] Keep every addition total, pure, deterministic, capability-free, and exactly typed; defer failable operations to `Result`.
- [x] Cover each operation with success and rejection tests, and specify the whole surface in `spec/types.md`.

Exit criterion met: `examples/tiered-discount.varn` expresses a tier-and-threshold rule as one condition, `and(gte(@1,1000),or(eq(@0.customerTier,"gold"),str.starts_with(@0.customerTier,"vip")))`, with no helper function per condition.

Also fixed here: `max`, `from`, `to`, and `in` became contextual keywords. They were reserved everywhere, so `max(3,9)` failed to parse and no record field could be called `max`. They now carry meaning only inside a `loop` or `each` header.

## Completed — packaging

- [x] Pack `Varn.Cli` as a .NET global tool, command `varn`.
- [x] Pack `Varn.ToolHost` as a .NET global tool, command `varn-mcp`, so MCP registration needs no clone or build.
- [x] Pack `Varn.Runtime`, `Varn.ModuleSdk`, and their dependencies as libraries, since embedding is the real use case.
- [x] Keep tests, benchmarks, and examples out of the feed by making nothing packable by default.
- [x] Verify the produced set in `scripts/pack.sh` and in CI, so a project that silently stops being packable fails the build.

Verified end to end at `0.1.0-alpha.1`: the packed `varn` tool installs and runs `examples/order-calculation.varn` with `--input`; the packed `varn-mcp` tool starts protocol-clean; and a fresh console app consuming `Varn.Runtime` and `Varn.Modules.Standard` from the local feed executes a record-and-result program against host input.

A smoke test of that last path caught `Varn.Syntax` failing to pack at all, because its project file was a single self-closing element that the packaging edit did not match. `scripts/pack.sh` now asserts the expected package set so the same class of silent omission fails loudly.

Still open: nothing is published to nuget.org. That needs an owner account, a signing decision, and a release workflow, all of which are outside this repository's automation today.

## Then — a browser playground

- [ ] Varn is .NET, so a WebAssembly build runs the whole check/inspect/run pipeline client-side with no backend and no abuse surface, which demonstrates the sandboxing claim rather than arguing it.

## Completed — benchmark harness

- [x] Four structured rule tasks with cases aimed at real traps: inclusive boundaries, division by an empty count, truncation toward zero, absent optionals, exact string matching.
- [x] Reference solutions plus a defect set in both languages, each recording the outcome it was written to have.
- [x] Outcome classification, exact grading, size measurement, and a committed report.
- [x] A self-check: the harness exits non-zero if any solution grades differently from its recorded intent, and runs in CI.

Findings, with the caveats in `bench/README.md` attached:

- On the eight defects written in both languages, Varn is strictly better on three, tied on five, worse on none. Paired silent-wrong count is **Varn 4, Python 6**.
- The three Varn catches are shape and type errors. The five ties are pure logic errors, which no type system catches, and they are the majority. **Varn's advantage is real and bounded.**
- Varn costs about **1.36x** the approximate tokens of Python, concentrated in `result` handling; the most failure-heavy task is 1.95x. The earlier prediction that Varn would lose badly on token cost was too pessimistic.
- The first revision of this benchmark overstated Varn by showing `case-insensitive` as not expressible, when Varn merely lacked `str.to_lower`. The function was added, the defect is now written in both languages, and it is a tie. Varn's paired silent-wrong count rose from 3 to 4 as a result.

## Delivered slices

Varn advances through small, measurable vertical slices. Each includes syntax/representation, static validation, runtime behavior, diagnostics, specifications, examples, and tests. Completed work is checked off here as it lands.

## Completed — typed host inputs

This slice separates the data from the program, so one checked Varn program is reusable across many host-supplied inputs.

- [x] Specify a declared program input contract and a structured entry-point result.
- [x] Accept host-supplied structured data as a checked value rather than generated source.
- [x] Validate every host input against the declared contract before execution begins.
- [x] Teach `varn_check` and `varn_run` the input contract, with exact rejection diagnostics.
- [x] Add tests that run one unchanged program over several different inputs.

Exit criterion met: `fn main(@0:Order)->Settlement` declares the contract, `varn_check` reports it as `contract.input`/`contract.result`, and `varn run --input` or the `varn_run` `input` argument supplies the data. `VARN6000`-`VARN6010` name every binding fault exactly, including list element paths such as `items[1]`. Binding precedes execution, so a rejected input consumes zero steps.

Protocol evidence: through the real stdio MCP process, one unchanged order-calculation program was checked once, its contract read from the response, then executed against three different inputs returning `235`, `0`, and `0` discounts; a mistyped element was rejected at `items[1]` with zero steps consumed and no capabilities granted.

Deliberately still open: the `varn_check` contract projection describes records only, because record fields cannot yet nest. Deeper host payloads need nested records, which is a separate slice.

## Completed — typed records

This slice makes structured application data explicit without introducing ambient object behavior.

- [x] Specify closed named-record type, construction, and field-access syntax.
- [x] Add immutable record values to the AST, checker, runtime, module SDK, JSON, and canonical format.
- [x] Require unique, ordinally ordered field names and exact field types.
- [x] Add exhaustive missing, duplicate, extra, access, and resource-accounting tests.
- [x] Exercise an AI-generated structured application task through the MCP adapter.

Exit criterion met: Varn validates and transforms a closed structured value with deterministic field order and no dynamic property access. `rec Order(items:list[i64],tier:str)` declares the shape, `rec[Order](...)` must set every declared field exactly once, `@0.items` is the only field read, and `VARN3036`–`VARN3044` name each structural fault precisely.

Protocol evidence: driven through the real stdio MCP process by `Varn.Adapter.Tests`, a record order-calculation program was rejected with `VARN3039` and `VARN3044`, repaired, inspected as `T[Order(items:list[i64];tier:str);Settlement(total:i64;discount:i64)]`, and executed to a deterministic `235` discount on a `2350` total with no capabilities granted. Source field order does not change the canonical projection, the result, or the step count. An autonomous Codex generate-check-repair-run session over records is still worth running before the next slice.

## Completed — typed lists

This slice enables the first meaningful bounded data-transformation benchmarks and builds directly on safe optional access.

- [x] Specify homogeneous list type and literal syntax.
- [x] Add immutable list values to the AST, checker, runtime, module SDK, and canonical format.
- [x] Provide deterministic length and safe indexed lookup returning an optional.
- [x] Add bounded traversal without implicit allocation or ambient mutation.
- [x] Add exhaustive size, element-type, index, and resource-accounting tests.
- [x] Compare an AI-generated list transformation through the MCP adapter.

Exit criterion met: Varn performs and verifies a bounded fold over a homogeneous list, with out-of-range access represented explicitly as absence.

Dogfood evidence: an isolated Codex agent generated a typed-list fold through the stdio MCP adapter, repaired `VARN3009` after its first check, inspected the canonical structure, and returned `14` in 29 deterministic steps with no capabilities granted.

## Completed — typed optional values

This slice represents absence without sentinel values or implicit null conversion.

- [x] Specify one explicit optional type and construction syntax.
- [x] Add optional type/value nodes to the lexer, parser, AST, and module SDK contract.
- [x] Require explicit presence checks before extracting a contained value.
- [x] Implement deterministic optional construction, branching, and inspection.
- [x] Add exhaustive success, type-mismatch, and unsafe-access rejection tests.
- [x] Exercise an optional-producing workflow through the MCP adapter.

Exit criterion met: checked Varn programs explicitly represent, inspect, and branch over present or absent typed values without unchecked access.

## Completed — explicit mutable slots

This slice makes bounded loops useful for deterministic transformations while keeping mutation visible and statically checked.

- [x] Specify distinct mutable declaration and assignment syntax; keep `let` immutable.
- [x] Add mutable-slot and assignment nodes to the lexer, parser, and AST.
- [x] Reject assignment to immutable, unknown, out-of-scope, or differently typed slots.
- [x] Execute scoped mutation with deterministic step accounting.
- [x] Extend canonical inspection, examples, and success/rejection tests.
- [x] Exercise a bounded-loop accumulator through the MCP adapter.

Exit criterion met: Varn computes an accumulator across a statically bounded loop, while every invalid mutation is rejected before execution.

## Completed — local AI tool adapter

The first adapter slice exposes Varn through a local Model Context Protocol server without giving the language core ambient host access.

- [x] Specify versioned requests for `check`, `inspect`, and `run`.
- [x] Implement a local tool host that delegates to the existing Varn engine.
- [x] Require explicit capabilities and resource ceilings for every execution request.
- [x] Add end-to-end adapter tests, including invalid source and denied capabilities.
- [x] Dogfood the adapter from Codex on a generate-check-repair-run example.

Exit criterion met: an AI agent can invoke all three operations through structured tool calls, receive schema-versioned results, and cannot execute an undeclared or ungranted capability.

## M0 — Repository ready for collaboration

- [x] Select the Apache License 2.0 and document the contribution policy.
- [x] Add GitHub CI for .NET 10 build, tests, and formatting.
- [x] Add issue and pull-request templates.
- [x] Publish the architectural invariants in an initial design record.

Exit criterion: a clean clone can run one documented command and receive the same result locally and in CI.

## M1 — Complete the small structured language

- [x] Add `if` with a required `bool` condition.
- [x] Add statically bounded loops with deterministic step accounting.
- [x] Add explicit mutable slots and assignment.
- [x] Add typed optional values with safe extraction.
- [x] Add typed lists with bounded traversal.
- [x] Add records.
- [x] Add typed host inputs.
- [x] Add `Result` values.
- [ ] Add exhaustive success and rejection tests for every feature.

Exit criterion: Varn can express useful deterministic transformations without ambient host access.

## M2 — Capability and resource verifier

- Replace flat capability strings with structured policies.
- Add domain/method/byte restrictions for network access.
- Add filesystem-root and operation restrictions.
- Add recursion, memory, output, and wall-clock host budgets.
- Define deterministic module error values and cancellation behavior.

Exit criterion: the verifier can explain all requested effects, capabilities, and resource ceilings before execution.

## M3 — AI-native representation and evaluation

- Version the canonical structural format.
- Add parse/format round-trip and canonical-equivalence tests.
- Build token-cost and generation-correctness benchmarks against Python, JavaScript, C#, Rust, and JSON IR.
- Add structural edit operations keyed by stable node identifiers.
- Publish reproducible eval datasets and results.

Exit criterion: Varn can demonstrate measurable AI-generation or verification advantages rather than relying on intuition.

## M4 — Bytecode and virtual machine

- Specify bytecode, verifier rules, and versioning.
- Compile the checked AST to bytecode.
- Execute bytecode in a deterministic VM.
- Add reproducible traces and differential tests against the interpreter.

Exit criterion: interpreter and VM agree on the complete conformance suite.

## M5 — Secure module ecosystem

- Version the module ABI and manifests.
- Add signed module packages and dependency metadata.
- Move untrusted integrations into an isolated module host.
- Build narrow standard modules for console, files, time, randomness, and HTTP.
- Add conformance tests and policy fixtures for module authors.

Exit criterion: integrations can be distributed without expanding the trusted language core.

## AI-usage track

In parallel with M1–M3:

1. [x] Keep the CLI stable and machine-readable.
2. [x] Add structured JSON diagnostics and execution results.
3. [x] Build a thin tool adapter around `check`, `inspect`, and `run`.
4. [ ] Package authoring guidance as a Codex skill after the syntax stabilizes.
5. [ ] Add an eval loop where an AI generates Varn, receives diagnostics, repairs it, and compares silent-wrong rate and tokens to verified-correct against Python.

This lets AI agents use Varn early while keeping the language experiment measurable.
