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
| S2 | Expressive enough for real rules | Standard library plus `Result`: a person can write the rule they actually came to write, and expected failures are values. | Done, confirmed by a dogfooding pass |
| S3 | Proven | A reproducible measurement states where Varn wins and where it loses. | Withdrawn: the first harness was removed, to be rebuilt once the surface is frozen |
| S4 | Embeddable | `dotnet tool install -g varn` and NuGet packages: somebody uses Varn without cloning this repository. | Built, unpublished |
| S5 | Connected | Structured network policy and a trusted HTTP module, so rules can reach real data under explicit grants. Covers M2. | Not started |
| S6 | Multi-tenant | Isolated, signed, versioned module processes, only if a real deployment demands them. See **Not planned**. | Not started |

Bytecode, a VM, and signed module isolation are cut rather than deferred; see **Not planned** for why. Do not start them because they are interesting.

Distribution (S4) intentionally comes after expressiveness (S2) and proof (S3). Shipping an easy install before a person can write `and` converts curiosity into a bad first impression, and first impressions are spent once.

## Current focus — make Varn cheaper to emit than Python

Varn only matters if a model that knows it produces correct programs for fewer tokens than the same model writing Python. Every item below is judged on **emitted tokens** and **generation error rate**, for a model that already knows the language.

Familiarity is explicitly not a criterion. The cost of reading the specification before writing a program is a bootstrap expense that exists once per model generation and disappears once the language is in training data; designing the surface around today's priors optimizes for a condition that expires. What is being removed is what is *human*-shaped: numeric slots, prefix arithmetic, ceremony restating a type declared three lines above, and diagnostics written as prose.

What stays untouched, because it is what makes the language worth training on at all: contracts on `main`, `result` as a value, capabilities, effects, step budgets, static field access, and a checker that refuses before execution.

### 1. Fix and freeze the surface

Everything downstream depends on the surface being final, so this comes first and nothing else starts until it is frozen.

- [x] **Named bindings replace numeric slots.** A name is decided once, at its binding. A slot number is global state the generator must carry through the whole function, and training does not remove that tax. `@` is gone from the language and reports `VARN1004`, which names the replacement. The canonical projection prints the binding's name; normalizing names to ordinals so two programs that differ only in naming project identically is the canonical-equivalence item under M3.
- [x] **Infix arithmetic and comparison.** `a * b` is three tokens; the call spelling it replaced was six, paid on every generation forever. Calls stay for everything else: infix is worth it only where precedence is universal and unambiguous. Every operator desugars to the call it replaces, so the checker, interpreter, canonical projection, and step budget were untouched; the call spelling now reports `VARN2008` and names the operator.
- [x] **Short-circuit `&&` and `||`,** removing the nested `if` ladders that exist only to avoid evaluating both operands. Prefix `!` came with them. Unlike arithmetic these cannot desugar to a call, so they are their own node through the checker, interpreter, and canonical projection. A step count now depends on the data, which `each` already made true; determinism is untouched.
- [x] **One form per concept.** No aliases, no second spelling. Choice costs deliberation and adds variance to generation. Enforced for bindings (`VARN1004`) and for every operator with a call spelling (`VARN2008`).
- [x] **Inference where the information is already in scope.** `none`, `list(...)`, and `err(...)` take their type from a `let`, a `var`, a `set` target, a `ret` against the declared return type, or a record field; a list also takes it from its first element. Writing one the context supplies reports `VARN3052`; nothing supplying one reports `VARN3051`. The type is settled during checking, so `err(reason)` and `err[Settlement](reason)` project identically.

**The surface is frozen.** Named bindings, infix arithmetic and comparison, short-circuit `&&`/`||` with prefix `!`, one form per concept, and inferred type arguments are all in. Changing it again means rewriting the skill and invalidating any corpus generated against it, so treat a further change as a decision to redo that work.

### 2. Ship Varn as a skill

The point of the language is that an agent spends fewer tokens getting a correct, checked rule than it would writing the same rule in a general-purpose language. A skill is how that reaches a real agent: it loads the language, hands over `check` and `run`, and is the thing to measure.

- [x] One document that is sufficient on its own to write correct Varn, kept as small as the frozen surface allows. `.claude/skills/varn/SKILL.md`, about 2,000 tokens against roughly 9,200 for `spec/`.
- [x] Worked examples covering the shapes that actually recur: a rule over a record, a fold over a list of records, an optional that must be checked, a failure carried as a value. Each ships the input JSON it documents, and the test suite runs all four.
- [x] The check-repair loop written down as a procedure, so a diagnostic leads to an edit rather than a regeneration, with a table mapping each common code to the edit it calls for.
- [x] Wire it to the existing MCP tools so the skill can check and run without leaving the session, registered by `scripts/register-codex-mcp.sh`.

Exit criterion: an agent with no prior exposure loads the skill and writes a correct rule on the first or second attempt, and the whole exchange costs fewer tokens than the same rule written and verified in Python.

**Built, unmeasured.** The card exists and every sample in it is verified by the test suite, but nobody has yet run the experiment the exit criterion describes: a fresh session, given only the card and a task, with no access to this repository. Until that is run, the claim that the skill saves tokens is a design argument, not a result. It needs no API key — a fresh session is a clean generator.

### 3. The oracle loop

The checker is a correctness oracle, which means Varn can manufacture its own training corpus. Nothing decides whether arbitrary Python is correct; `varn check` plus a set of cases decides Varn exactly. This is the only mechanism by which "a model will know Varn" becomes true rather than hopeful, and it is not available to any language whose correctness is undecidable.

- [ ] Generate (task, program, verdict) triples mechanically against the frozen surface.
- [ ] Label every triple with the checker's verdict and the graded case outcomes.
- [ ] Keep each rejected program paired with its diagnostic and its repair, so failures carry signal too.
- [ ] Publish the dataset and the generator that produced it.

Exit criterion: a reproducible corpus large enough to train on, produced without a human labelling a single example.

### 4. Structured diagnostics, and repair as a patch

- [ ] Diagnostics carry a machine-applicable payload — code, node, and the specific fault — rather than a sentence written for a person.
- [ ] Structural edit operations keyed by stable node identifiers.
- [ ] A repair costs an edit, not a regenerated program.

Exit criterion: fixing a one-field defect costs an order of magnitude fewer tokens than regenerating the program that contains it.

### Later — rebuild the measurement

The first benchmark — six rule tasks, hand-written defects in Varn and Python, and a grading harness — was removed along with its Python arm. It measured *mechanism*, which defects each language's checker can catch, and never *frequency*, which is how often a generator actually makes each one. Rebuilding it belongs after the surface is frozen and the skill exists, because both change what there is to measure.

Its last reading, for the record and with all the caveats it carried: on sixteen defects written in both languages Varn was strictly better on seven, tied on nine, worse on none, and cost about 1.17x the proxy tokens of Python. Treat that as a starting point to re-derive, not as a result to cite.

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

Varn exposed nine callable names and could not express `and`, which blocked real rules and any honest comparison.

- [x] Add total boolean operations: `and`, `or`, `not`.
- [x] Complete the comparison set: `gt`, `lte`, `gte`, `ne` over the exact types that already support `eq` and `lt`.
- [x] Add the arithmetic a rule needs: `mod`, `abs`, `min`, `max`.
- [x] Add ordinal string operations: length, concatenation, containment, and prefix/suffix tests.
- [x] Add `list.contains` for every supported element type.
- [x] Keep every addition total, pure, deterministic, capability-free, and exactly typed; defer failable operations to `Result`.
- [x] Cover each operation with success and rejection tests, and specify the whole surface in `spec/types.md`.

Exit criterion met: `examples/tiered-discount.varn` expresses a tier-and-threshold rule as one condition, `total >= 1000 && (order.customerTier == "gold" || str.starts_with(order.customerTier,"vip"))`, with no helper function per condition.

Also fixed here: `max`, `from`, `to`, and `in` became contextual keywords. They were reserved everywhere, so `max(3,9)` failed to parse and no record field could be called `max`. They now carry meaning only inside a `loop` or `each` header.

## Completed — dogfooding pass

Varn was used to write ordinary programs — payroll with overtime, an over-limit receipt, a cart of line items, a filter, a tax table — and every point of friction was recorded and then fixed. Five gaps surfaced, all of which the author had been working around by hand without noticing.

- [x] **A value could not be put into a message.** `str.to_i64` parsed strings into numbers, but nothing went the other way, so every failure reason had to be a constant. For a language whose pitch is that failures carry reasons, that was the worst of the five. Added `str.from_i64`, `str.from_f64`, `str.from_bool`.
- [x] **Every function needed an unreachable trailing `ret`.** `if ... ret ... else ... ret ... end` was rejected, so programs ended in lies like `ret err("unreachable")` — which this repository's own examples did. The checker now accepts a body whose every branch returns; a loop still does not count, because it may run zero times. The unreachable lines are gone from the examples.
- [x] **A record could not contain a record, and a list could not hold one.** Line items, batches, addresses — the most common shapes in real data — were inexpressible. A record field, list element, and optional may now each hold a declared record, with nesting stopping at one level. Recursive records are rejected with `VARN3049`.
- [x] **Lists were construct-only.** There was no way to build one, so `each` could only fold to a scalar and no transformation could produce a collection. Added `list.append`, which returns a new list.
- [x] **Chained field access did not parse.** Found by a test written for the nesting work: the lexer folds dots into identifiers so `io.print` stays one token, which made `order.home.city` arrive as a single `home.city` identifier. The parser now splits it, which is unambiguous because field names may not contain dots.

The input binder, contract projection, canonical format, JSON results, and MCP guidance all follow the relaxed type rules. `contract.records` now carries every declared shape, so a host can resolve a nested or list element type name without parsing source.

What this said about the language: before the relaxed rules, a Varn program could only take scalars in and return scalars out, which is not the shape host data has. Lists of records, nested records, and optional records are what real rules read, and they are also where a declared contract catches the most — a misnamed nested field, a missing list-valued output field, an unguarded optional record are all refused before execution.

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

## Removed — benchmark harness

Built, used, and then deleted along with its Python arm. It graded hand-written solutions by how each language *fails* — rejected before execution, crashed, or silently wrong — over six small rule tasks.

What it established, and what should be re-derived rather than assumed when it is rebuilt: Varn converts type and shape faults into rejections before execution, ties on pure logic errors because no type system catches those, and pays a token premium concentrated in `result` handling. It measured mechanism, never frequency, because no model was ever called. See **Later — rebuild the measurement**.

## Delivered slices

Varn advances through small, measurable vertical slices. Each includes syntax/representation, static validation, runtime behavior, diagnostics, specifications, examples, and tests. Completed work is checked off here as it lands.

## Completed — typed host inputs

This slice separates the data from the program, so one checked Varn program is reusable across many host-supplied inputs.

- [x] Specify a declared program input contract and a structured entry-point result.
- [x] Accept host-supplied structured data as a checked value rather than generated source.
- [x] Validate every host input against the declared contract before execution begins.
- [x] Teach `varn_check` and `varn_run` the input contract, with exact rejection diagnostics.
- [x] Add tests that run one unchanged program over several different inputs.

Exit criterion met: `fn main(order:Order)->Settlement` declares the contract, `varn_check` reports it as `contract.input`/`contract.result`, and `varn run --input` or the `varn_run` `input` argument supplies the data. `VARN6000`-`VARN6010` name every binding fault exactly, including list element paths such as `items[1]`. Binding precedes execution, so a rejected input consumes zero steps.

Protocol evidence: through the real stdio MCP process, one unchanged order-calculation program was checked once, its contract read from the response, then executed against three different inputs returning `235`, `0`, and `0` discounts; a mistyped element was rejected at `items[1]` with zero steps consumed and no capabilities granted.

Deliberately still open: the `varn_check` contract projection describes records only, because record fields cannot yet nest. Deeper host payloads need nested records, which is a separate slice.

## Completed — typed records

This slice makes structured application data explicit without introducing ambient object behavior.

- [x] Specify closed named-record type, construction, and field-access syntax.
- [x] Add immutable record values to the AST, checker, runtime, module SDK, JSON, and canonical format.
- [x] Require unique, ordinally ordered field names and exact field types.
- [x] Add exhaustive missing, duplicate, extra, access, and resource-accounting tests.
- [x] Exercise an AI-generated structured application task through the MCP adapter.

Exit criterion met: Varn validates and transforms a closed structured value with deterministic field order and no dynamic property access. `rec Order(items:list[i64],tier:str)` declares the shape, `rec[Order](...)` must set every declared field exactly once, `order.items` is the only field read, and `VARN3036`–`VARN3044` name each structural fault precisely.

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

Superseded by the current focus. The surviving items live there; these two remain because nothing above covers them.

- [ ] Version the canonical structural format.
- [ ] Add parse/format round-trip and canonical-equivalence tests.

## Not planned

Cut deliberately, and not merely deferred: **bytecode and a virtual machine**, **signed module packages**, and **isolated module host processes**.

None of them makes a generator cheaper or more accurate, which is the only thing that decides whether this language is worth using. Every hour spent there is an hour not spent on the five items above. Revisit only when measurement demands it — a profile showing the interpreter is the bottleneck on a real workload, or a deployment that genuinely cannot run trusted modules in-process. Not because they are interesting.

## AI-usage track

In parallel with M1–M3:

1. [x] Keep the CLI stable and machine-readable.
2. [x] Add structured JSON diagnostics and execution results.
3. [x] Build a thin tool adapter around `check`, `inspect`, and `run`.
4. [ ] Package authoring guidance as a skill after the surface freezes. This is now item 2 of the current focus.
5. [ ] Add an eval loop where an AI generates Varn, receives diagnostics, and repairs it, counting tokens to verified-correct. Belongs with the rebuilt measurement.

This lets AI agents use Varn early while keeping the language experiment measurable.
