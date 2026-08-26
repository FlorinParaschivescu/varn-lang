# Varn repository instructions

These instructions apply to every task in this repository.

## Toolchain

- Use the SDK pinned by `global.json`.
- Target .NET 10 and stable C# 14.
- Use `Varn.slnx`; do not recreate a legacy `.sln` file.

## Before completing a change

Run:

```sh
dotnet build Varn.slnx
dotnet run --project tests/Varn.Tests --no-build
dotnet format Varn.slnx --verify-no-changes --no-restore
```

For CLI changes, also run `check`, `inspect`, and `run` against `examples/hello.varn`. For module changes, build and load `Varn.ExampleModule` through `--module`.

## Architecture rules

- Implement language features as complete vertical slices: syntax, lexer/parser, validation, runtime, diagnostics, specification, and tests.
- Keep optional integrations out of the language core. Host integrations belong behind `Varn.ModuleSdk`.
- Preserve ordinal identifier comparison, invariant numeric behavior, explicit effects, explicit capabilities, and deterministic resource accounting.
- A program capability declaration never grants access; the host must independently authorize it.
- Do not load or recommend untrusted .NET module assemblies. Modules execute as trusted host code until process isolation exists.
- Keep the canonical formatter deterministic. Do not depend on hash-map iteration order or current culture.
- Avoid speculative subsystems. Prefer the smallest end-to-end feature that advances the current roadmap milestone.

Read the relevant files in `spec/` and `ROADMAP.md` before changing language behavior.
