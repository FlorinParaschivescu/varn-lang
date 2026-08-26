# Contributing to Varn

Varn is at the bootstrap stage. Small, end-to-end changes are more useful than broad speculative subsystems.

## Before changing code

1. Read the files in `spec/` that cover your change.
2. Keep module-specific dependencies out of the lexer, parser, type system, and runtime.
3. Decide what the checker can reject before execution.
4. Add a test that fails without the change.

## Build and test

The project targets .NET 10 with C# 14 and uses no third-party runtime or test packages.

```sh
dotnet build Varn.slnx
dotnet run --project tests/Varn.Tests --no-build
```

PowerShell users can run `./scripts/test.ps1`; Unix-like shells can run `./scripts/test.sh`.

## Change shape

A language change normally touches `Varn.Syntax`, `Varn.Lexer`, `Varn.Parser`, `Varn.TypeSystem`, `Varn.Runtime`, the specification, and tests. A host integration should normally be a module that references `Varn.ModuleSdk`, not a dependency of the core runtime.

Use ordinal comparison for identifiers and capability/effect names, invariant culture for numeric parsing and formatting, and stable ordering in canonical output. Make implicit conversion and ambient host access errors.

Keep pull requests focused, describe observable behavior, list any new diagnostics, and include the commands used to verify the change.

## Contribution terms

Varn is distributed under the [Apache License 2.0](LICENSE). Unless explicitly stated otherwise, an intentionally submitted contribution is provided under the same license, as described by section 5 of Apache-2.0. Contributors must have the right to submit their work and should identify third-party code or assets in the pull request.
