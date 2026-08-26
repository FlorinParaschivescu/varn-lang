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

This runs 90 language/runtime tests and 10 adapter/MCP protocol tests. The language bootstrap test runner has no third-party test framework and can also be run directly:

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

## Structured values

```varn
budget[steps=300]

rec Order(items:list[i64],tier:str)
rec Settlement(total:i64,discount:i64)

fn settle(@0:Order)->Settlement
    let @1:i64 total(@0.items)
    ret rec[Settlement](total=@1,discount=discount(@1,@0.tier))
end
```

A `rec` declaration is closed and immutable. Construction must set every declared field exactly once, missing, extra, duplicate, and mistyped fields each get their own diagnostic, and `@0.items` is the only way to read a field. Declaration order is the only field order the runtime, canonical projection, and JSON results use. See [examples/typed-records.varn](examples/typed-records.varn).

## Writing a rule

Varn has no operators. Every operation is a call, and the standard library is small enough to hold in context:

```varn
if and(gte(@1,1000),or(eq(@0.customerTier,"gold"),str.starts_with(@0.customerTier,"vip")))
    ret rec[Settlement](total=@1,discount=div(mul(@1,10),100),note=str.concat("tier ",@0.customerTier))
end
```

`add sub mul div mod min max abs` for `i64`/`f64`, `and or not` for `bool`, `eq ne` for every scalar, `lt gt lte gte` for `i64`/`f64`/`str`, `str.length str.concat str.contains str.starts_with str.ends_with`, and `list.length list.get list.contains`. All total, pure, and exactly typed, with no implicit conversions. See [examples/tiered-discount.varn](examples/tiered-discount.varn) and [the type contract](spec/types.md).

## Expected failure is a value

```varn
fn main(@0:Order)->result[Settlement]
    if ok @3:i64 rate(@0.customerTier)
        ret ok(rec[Settlement](total=@1,discount=@3))
    else err @6:str
        ret err[Settlement](@6)
    end
end
```

`result[T]` carries a success value or a `str` reason, `ok`/`err[T]` construct it, and `if ok ... else err ...` is the only way to read either side. A program that returns a failure **ran correctly**, so the response reports `success` with no diagnostics and the reason in `returnValue`:

```json
{"success":true,"exitCode":1,"returnValue":{"type":"result[Settlement]",
  "value":{"ok":false,"value":null,"error":{"type":"str","value":"unknown tier: platinum"}}}}
```

That distinction is the point: a rule that did not hold is not a broken run. `num.div`, `num.mod`, `num.to_i64`, `str.to_i64`, and `str.to_f64` return results for the same reason. See [examples/rule-with-failure.varn](examples/rule-with-failure.varn).

## Host input: one program, many inputs

Give `main` a record parameter and the data arrives from the host instead of the source:

```varn
fn main(@0:Order)->Settlement
```

```powershell
./varn.cmd check examples/order-calculation.varn --json
./varn.cmd run examples/order-calculation.varn --input examples/order-gold.json --json
./varn.cmd run examples/order-calculation.varn --input examples/order-basic.json --json
```

The gold input returns `{total:2350, discount:235}` and the basic input returns `{total:140, discount:0}` from the same unchanged, already-checked program. No string interpolation, no regeneration per input.

`check --json` reports the contract a host must satisfy:

```json
"contract":{"input":{"type":"Order","fields":[{"name":"items","type":"list[i64]"},{"name":"customerTier","type":"str"}]},"result":"Settlement"}
```

Input is bound and validated before execution begins, so a rejected input consumes zero steps and never partially runs. Binding is exact: no coercion, no defaults, no extra keys, and `1200.5` is rejected for an `i64` rather than truncated. See [the tooling contract](spec/tooling.md).

## CLI

```powershell
./varn.cmd check examples/hello.varn
./varn.cmd inspect examples/hello.varn
./varn.cmd run examples/hello.varn --allow console.write
./varn.cmd run examples/order-calculation.varn --input examples/order-gold.json
```

- `check` parses and validates without executing.
- `inspect` emits a deterministic structural projection of the validated AST.
- `run` validates, binds any `--input` to the declared record, applies host policy and resource limits, then interprets `main`.

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

- Available now: AI syntax generation, stable-diagnostic repair, canonical inspection, deterministic execution, module/API contract experiments, small bounded typed-list transformations, closed structured records, and one verified program reused across many structured host inputs.
- Next: a reproducible benchmark against Python plus JSON tool calls, measuring silent-wrong rate and tokens to verified-correct.
- After structured network policy and a trusted HTTP module: controlled webpage and API experiments.
- After process isolation: community execution of modules that are not already trusted host code.

The current tests are real end-to-end protocol tests, but Varn remains too small for a representative application benchmark. The roadmap treats each readiness gate as an explicit deliverable.

## Modules first

Extensible operations such as `add`, `eq`, and `io.print` are typed functions registered by standard modules through `Varn.ModuleSdk`; only small pure structural operations such as safe list access live in the language runtime. A module supplies a stable name, typed functions, and the explicit effect and capability required by each effectful function.

Modules can be embedded with `engine.AddModule(...)` or explicitly loaded by the CLI:

```sh
varn run program.varn --module path/to/MyModule.dll --allow my.capability
```

This is the intended path for future web access: a network module can expose narrow functions such as `net.get` without adding HTTP access to the language core. See [the module contract](spec/modules.md).

> A loaded .NET module is trusted host code and is not sandboxed by Varn. Capability checks control whether Varn programs may call it; they cannot prevent a malicious module assembly from using .NET directly. Only load modules you trust. The MCP adapter deliberately does not load external assemblies.

## Benchmark

```sh
dotnet run --project bench/Varn.Bench
```

Four structured rule tasks, reference solutions and a defect set in Varn and Python, graded by how each language *fails*: rejected before execution, crashed, or silently wrong. On the seven defects written in both languages Varn is strictly better on three, tied on four, and worse on none, converting type and shape errors into rejections. The ties are pure logic errors, which no type system catches, and they are the majority. Varn costs about 1.36x the tokens.

No model is called, so this measures mechanism rather than frequency. [bench/README.md](bench/README.md) states what the numbers do and do not show, and where model-generated solutions plug in.

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
bench/                    task set, solutions, and the grading harness
spec/                     current language, tooling, adapter, and extension contracts
```

## v0.1 boundary

Implemented now: explicit `result` values for expected failures; a standard library of arithmetic, boolean, comparison, string, list, conversion, and parsing operations; scalar literals; typed functions; explicit immutable and mutable numeric slots; statically checked assignment; typed optional construction and safe extraction; immutable homogeneous lists with bounded traversal and safe indexing; closed immutable records with exact construction and static field access; validated structured host input and structured results; user and module calls; arithmetic and comparisons; typed conditions; statically bounded loops; explicit effects and capabilities; separate host grants; step budgets; console output; deterministic inspection; structured JSON results; external module loading; and a policy-gated local MCP adapter.

Intentionally deferred: structured result failure types, bytecode, the VM, richer resource models, a final binary/token canonical encoding, signed module manifests, and process/OS sandboxing.

See [ROADMAP.md](ROADMAP.md) for the living sequence. Features are added one vertical slice at a time, with checker, runtime, interfaces, specification, and tests updated together.

## Status

Varn is pre-alpha research software. Its syntax, JSON contract, MCP tools, and module ABI may change with explicit versioning. Varn is licensed under the [Apache License 2.0](LICENSE).

See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow.
