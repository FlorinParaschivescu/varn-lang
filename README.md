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

This runs 33 language/runtime tests and 6 adapter/MCP protocol tests. The language bootstrap test runner has no third-party test framework and can also be run directly:

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

## Use Varn from Codex or another MCP client

Varn includes a local stdio MCP server with structured `varn_check`, `varn_inspect`, and `varn_run` tools. On Windows, build and register it with Codex using:

```powershell
./scripts/register-codex-mcp.ps1
```

On Unix-like systems:

```sh
./scripts/register-codex-mcp.sh
```

Restart the Codex client after registration and use `/mcp` or the MCP settings page to confirm that `varn` is connected. The registration scripts modify the current user's Codex MCP configuration only when explicitly run.

The adapter accepts source text rather than paths, loads no external assemblies, and requires explicit capability, step, and output ceilings for every execution. See [the adapter contract](spec/adapter.md).

## How an AI agent uses Varn

An agent calls `varn_check` with generated source, repairs stable diagnostics, optionally calls `varn_inspect`, then calls `varn_run` only when execution is required. The run request must grant the smallest capability set and supply bounded resources.

The adapter has been exercised through real Codex check–repair–inspect–run workflows as well as the official MCP C# client. `AGENTS.md` gives Codex durable project-specific build, test, and safety instructions.

## Practical test readiness

- Available now: AI syntax generation, stable-diagnostic repair, canonical inspection, deterministic execution, and module/API contract experiments.
- After typed lists: small bounded data-transformation benchmarks against other languages.
- After records and `Result`: practical structured workflows with explicit failure handling.
- After structured network policy and a trusted HTTP module: controlled webpage and API experiments.
- After process isolation: community execution of modules that are not already trusted host code.

The current tests are real end-to-end protocol tests, but Varn remains too small for a representative application benchmark. The roadmap treats each readiness gate as an explicit deliverable.

## Modules first

Language primitives are not hard-wired into the interpreter. `add`, `eq`, and `io.print` are typed functions registered by standard modules through `Varn.ModuleSdk`. A module supplies a stable name, typed functions, and the explicit effect and capability required by each effectful function.

Modules can be embedded with `engine.AddModule(...)` or explicitly loaded by the CLI:

```sh
varn run program.varn --module path/to/MyModule.dll --allow my.capability
```

This is the intended path for future web access: a network module can expose narrow functions such as `net.get` without adding HTTP access to the language core. See [the module contract](spec/modules.md).

> A loaded .NET module is trusted host code and is not sandboxed by Varn. Capability checks control whether Varn programs may call it; they cannot prevent a malicious module assembly from using .NET directly. Only load modules you trust. The MCP adapter deliberately does not load external assemblies.

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
  Varn.Adapter/           protocol-neutral AI tool service and host policy
  Varn.ToolHost/          local MCP stdio server
tests/
  Varn.Tests/             dependency-free language/runtime test runner
  Varn.Adapter.Tests/     adapter policy and MCP protocol test runner
examples/                 programs and an external module
spec/                     current language, tooling, adapter, and extension contracts
```

## v0.1 boundary

Implemented now: scalar literals; typed functions; explicit immutable and mutable numeric slots; statically checked assignment; typed optional construction and safe extraction; user and module calls; arithmetic and comparisons; typed conditions; statically bounded loops; explicit effects and capabilities; separate host grants; step budgets; console output; deterministic inspection; structured JSON results; external module loading; and a policy-gated local MCP adapter.

Intentionally deferred: lists, records, `Result`, bytecode, the VM, richer resource models, a final binary/token canonical encoding, signed module manifests, and process/OS sandboxing.

See [ROADMAP.md](ROADMAP.md) for the living sequence. Features are added one vertical slice at a time, with checker, runtime, interfaces, specification, and tests updated together.

## Status

Varn is pre-alpha research software. Its syntax, JSON contract, MCP tools, and module ABI may change with explicit versioning. Varn is licensed under the [Apache License 2.0](LICENSE).

See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow.
