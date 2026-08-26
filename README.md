# Varn

Varn is an experimental machine-native programming language and runtime designed primarily for programs produced, consumed, checked, and modified by AI systems.

The research question is: **what program representation gives AI systems the lowest ambiguity and token cost while maximizing verifiability, reproducibility, and safe execution?**

This repository contains the .NET 10/C# 14 bootstrap implementation. It runs a deliberately small v0.1 language through a real pipeline:

```text
source -> lexer -> parser -> AST -> type/effect/capability checker -> interpreter
```

## Requirements

Install the .NET 10 SDK. The repository pins the tested SDK feature band in `global.json` and uses the modern `Varn.slnx` solution format.

## Test everything

On Windows PowerShell:

```powershell
./scripts/test.ps1
```

On Unix-like systems:

```sh
./scripts/test.sh
```

The bootstrap test runner has no third-party packages and can also be run directly:

```sh
dotnet run --project tests/Varn.Tests
```

## First program

```varn
cap[console.write]
budget[steps=100]

fn main()->i64 ![console]
    let @0:i64 10
    let @1:i64 20
    let @2:i64 add(@0,@1)
    io.print(@2)
    ret 0
end
```

On Windows, use the repository launcher:

```powershell
./varn.cmd run examples/hello.varn --allow console.write
```

Or use the .NET CLI directly on any platform:

```sh
dotnet run --project src/Varn.Cli -- run examples/hello.varn --allow console.write
```

Expected output: `30`.

The program must declare `console.write`; the host must independently grant it with `--allow console.write`. Declaring a capability never grants it.

## CLI

```powershell
./varn.cmd check examples/hello.varn
./varn.cmd inspect examples/hello.varn
./varn.cmd run examples/hello.varn --allow console.write
```

- `check` parses and validates without executing.
- `inspect` emits a deterministic structural projection of the validated AST.
- `run` validates, applies host policy and resource limits, then interprets `main`.

Add `--json` to any command for a versioned, machine-readable response:

```powershell
./varn.cmd check examples/hello.varn --json
./varn.cmd inspect examples/control-flow.varn --json
./varn.cmd run examples/hello.varn --allow console.write --json
```

In JSON run mode, program output is captured in the `output` property, so stdout contains one JSON document and can be consumed safely by agents or other tools. See [the tooling contract](spec/tooling.md).

## How an AI agent uses Varn

An agent with repository shell access can generate a `.varn` file, invoke `check --json`, repair any reported diagnostics, optionally inspect the canonical structure, and invoke `run --json` with an explicit host capability policy. `AGENTS.md` gives Codex durable project-specific build, test, and safety instructions.

The next integration layer is a thin adapter over this JSON contract. It can expose Varn as a Codex skill or MCP tool without coupling the language core to any one AI platform.

## Modules first

Language primitives are not hard-wired into the interpreter. `add`, `eq`, and `io.print` are typed functions registered by standard modules through `Varn.ModuleSdk`. A module supplies a stable name, typed functions, and the explicit effect and capability required by each effectful function.

Modules can be embedded with `engine.AddModule(...)` or explicitly loaded by the CLI:

```sh
varn run program.varn --module path/to/MyModule.dll --allow my.capability
```

This is the intended path for future web access: a network module can expose narrow functions such as `net.get` without adding HTTP access to the language core. See [the module contract](spec/modules.md).

> A loaded .NET module is trusted host code and is not sandboxed by Varn. Capability checks control whether Varn programs may call it; they cannot prevent a malicious module assembly from using .NET directly. Only load modules you trust.

## Repository map

```text
Varn.slnx
src/
  Varn.Syntax/            AST, source spans, diagnostics, types
  Varn.Lexer/             deterministic tokenization
  Varn.Parser/            source to AST
  Varn.ModuleSdk/         public module contract and values
  Varn.TypeSystem/        types, effects, and capability validation
  Varn.Runtime/           engine, interpreter, budgets, inspection, JSON results
  Varn.Modules.Standard/  arithmetic, comparison, and console modules
  Varn.Cli/               check / inspect / run
tests/Varn.Tests/         dependency-free bootstrap test runner
examples/                 programs and an external module
spec/                     current language, tooling, and extension contracts
```

## v0.1 boundary

Implemented now: scalar literals; typed functions; immutable numeric slots; user and module calls; arithmetic and comparisons; typed conditions; statically bounded loops; explicit effects and capabilities; separate host grants; step budgets; console output; deterministic inspection; structured JSON results; and external module loading.

Intentionally deferred: mutation, lists, records, optionals, `Result`, bytecode, the VM, richer resource models, a final binary/token canonical encoding, signed module manifests, and process/OS sandboxing.

See [ROADMAP.md](ROADMAP.md) for the proposed sequence. Features should be added one vertical slice at a time, with checker, runtime, CLI, specification, and tests updated together.

## Status

Varn is pre-alpha research software. Its syntax, JSON contract, and module ABI may change with explicit versioning. The repository does not yet include a license; one must be selected before accepting outside contributions or publishing a release.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow.
