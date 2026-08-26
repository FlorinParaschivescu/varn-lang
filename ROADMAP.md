# Varn roadmap

Varn should advance through small, measurable vertical slices. Each milestone includes syntax/representation, static validation, runtime behavior, diagnostics, specifications, examples, and tests. Completed work is checked off in this file as it lands.

## Current focus — explicit mutable slots

The next vertical slice makes bounded loops useful for deterministic transformations while keeping mutation visible and statically checked.

- [ ] Specify distinct mutable declaration and assignment syntax; keep `let` immutable.
- [ ] Add mutable-slot and assignment nodes to the lexer, parser, and AST.
- [ ] Reject assignment to immutable, unknown, out-of-scope, or differently typed slots.
- [ ] Execute scoped mutation with deterministic step accounting.
- [ ] Extend canonical inspection, examples, and success/rejection tests.
- [ ] Exercise a bounded-loop accumulator through the MCP adapter.

Exit criterion: Varn can compute an accumulator across a statically bounded loop, while every invalid mutation is rejected before execution.

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
- [ ] Add explicit mutable slots and assignment.
- [ ] Add optionals, lists, records, and `Result` values one at a time.
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
5. [ ] Add an eval loop where an AI generates Varn, receives diagnostics, repairs it, and compares token cost and success rate.

This lets AI agents use Varn early while keeping the language experiment measurable.
