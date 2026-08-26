# Varn roadmap

Varn should advance through small, measurable vertical slices. Each milestone includes syntax/representation, static validation, runtime behavior, diagnostics, specifications, examples, and tests.

## M0 — Repository ready for collaboration

- [ ] Select an open-source license and contribution policy.
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
3. [ ] Build a thin tool adapter around `check`, `inspect`, and `run`.
4. [ ] Package authoring guidance as a Codex skill after the syntax stabilizes.
5. [ ] Add an eval loop where an AI generates Varn, receives diagnostics, repairs it, and compares token cost and success rate.

This lets AI agents use Varn early while keeping the language experiment measurable.
