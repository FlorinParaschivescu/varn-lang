# Varn roadmap

Varn should advance through small, measurable vertical slices. Each milestone includes syntax/representation, static validation, runtime behavior, diagnostics, specifications, examples, and tests. Completed work is checked off in this file as it lands.

## Practical test gates

1. [x] Real agent protocol loop: Codex generates, checks, repairs, inspects, and executes Varn through MCP.
2. [x] Small data transformations: typed lists enable bounded map/filter/fold comparisons.
3. [x] Structured application tasks: records and typed host inputs run one verified rule over many structured inputs. `Result` still owes explicit failure contracts.
4. [ ] Controlled web/API tasks: structured network policy plus a trusted HTTP module.
5. [ ] Community-hosted execution: isolated, versioned, signed module processes.

## Current focus — measure the thesis

Varn now runs the first scenario worth benchmarking, so the next slice measures whether the representation actually earns its cost instead of assuming it does. This is deliberately sequenced before `Result`: the outcome decides whether the rest of the roadmap is aimed correctly.

- [ ] Build a reproducible task set of small structured rules with known-correct answers.
- [ ] Generate solutions in Varn and in Python-plus-JSON-tool-calls under identical conditions.
- [ ] Measure **silent-wrong rate**: the run succeeds and returns a plausible but incorrect answer.
- [ ] Measure **tokens to verified-correct**, counting every repair cycle, not tokens to first output.
- [ ] Measure first-try correctness and correctness after N repairs, and publish the dataset and results.

Exit criterion: a reproducible benchmark states where Varn wins and where it loses. The expected shape is that Varn loses on raw token cost, because models have seen no Varn and it has no operators, and wins on silent-wrong rate, because types are exact and nothing is coerced. If that holds, the research question should be rewritten around verifiability rather than token cost.

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
- [ ] Add `Result` values.
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
