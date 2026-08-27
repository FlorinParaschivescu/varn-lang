# Varn repository instructions

These instructions apply to every task in this repository.

## Toolchain

- Use the SDK pinned by `global.json`.
- Target .NET 10 and stable C# 14.
- Use `Varn.slnx`; do not recreate a legacy `.sln` file.

## Before completing a change

Run the gate, which mirrors `.github/workflows/ci.yml`:

```sh
sh ./scripts/test.sh
```

It restores from `Varn.slnx` rather than from a project, because that is what catches a solution entry pointing at a project that no longer exists. Keep it in step with CI: anything CI checks that the script does not is a gap that lets a push break `main`.

For CLI changes, also run `check`, `inspect`, and `run` against `examples/hello.varn`. For module changes, build and load `Varn.ExampleModule` through `--module`. For adapter changes, exercise the real stdio MCP process through `Varn.Adapter.Tests`.

## Architecture rules

- Implement language features as complete vertical slices: syntax, lexer/parser, validation, runtime, diagnostics, specification, interfaces, and tests.
- Keep optional integrations out of the language core. Host integrations belong behind `Varn.ModuleSdk`; agent protocols belong outside the runtime.
- Preserve ordinal identifier comparison, invariant numeric behavior, explicit effects, explicit capabilities, and deterministic resource accounting.
- A program capability declaration never grants access; the host must independently authorize it.
- Do not load or recommend untrusted .NET module assemblies. Modules execute as trusted host code until process isolation exists.
- Keep the canonical formatter deterministic. Do not depend on hash-map iteration order or current culture.
- Keep MCP stdout protocol-clean. Send logs to stderr and require explicit execution ceilings.
- Add new `.sh` scripts with the executable bit set in git (`git update-index --chmod=+x`). Windows checkouts do not preserve it, and CI invokes scripts directly.
- Avoid speculative subsystems. Prefer the smallest end-to-end feature that advances the current roadmap milestone.

Read the relevant files in `spec/` and `ROADMAP.md` before changing language behavior.
