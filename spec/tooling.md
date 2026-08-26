# Varn tooling contract

Varn's command-line interface is both a human interface and the first machine integration boundary. Pass `--json` to `check`, `inspect`, or `run` to receive exactly one JSON document on standard output.

The current `schemaVersion` is `1`. Consumers must reject unsupported versions instead of guessing their meaning. Additive fields may be introduced within a version; removing a field or changing its meaning requires a new version.

## Common fields

Every response contains:

- `schemaVersion`: integer contract version.
- `command`: `check`, `inspect`, `run`, or `unknown` for an argument error before a command is recognized.
- `success`: whether the requested operation succeeded.
- `diagnostics`: an array of diagnostics. Each item contains a stable `code`, a human-readable `message`, and a one-based source `span` with `line` and `column`. CLI argument errors use `VARN0001` and the sentinel span `0:0`.

The process exit status remains meaningful: `0` means success, `1` means the Varn program was rejected or failed, and `2` means command-line usage or host I/O failed. A successful `run` returns the program's `i64` result as its process exit code.

## Check

```powershell
./varn.cmd check examples/hello.varn --json
```

```json
{"schemaVersion":1,"command":"check","success":true,"diagnostics":[]}
```

## Inspect

`inspect` adds `canonical`. It contains the deterministic structural projection for a valid program and is `null` when validation fails.

```powershell
./varn.cmd inspect examples/control-flow.varn --json
```

## Run

`run` adds:

- `exitCode`: the program result expressed as a process exit code.
- `steps`: deterministic interpreter steps consumed.
- `returnValue`: `null` or an object with `type` and `value`. A present optional value nests another typed value object; an absent optional has a `null` value. A list value is an array of recursively typed value objects. A record value is an array of `{"name","value"}` objects in declared field order, where each `value` is itself a typed value object; an array preserves field order without depending on JSON object key ordering.
- `output`: all program output captured as text.

```powershell
./varn.cmd run examples/hello.varn --allow console.write --json
```

```json
{"schemaVersion":1,"command":"run","success":true,"exitCode":0,"steps":8,"returnValue":{"type":"i64","value":0},"output":"30\r\n","diagnostics":[]}
```

The exact step count and line ending in this illustration are not a compatibility promise. Their types and meanings are. JSON mode never mixes program output with the response document.

## Recommended agent loop

1. Write a candidate `.varn` source file.
2. Call `check --json` and branch on `success`.
3. Repair source using diagnostic codes and spans, then check again.
4. Optionally call `inspect --json` to compare canonical structure.
5. Call `run --json` only with the smallest required `--allow` grants and a suitable `--max-steps` ceiling.
6. Treat external assemblies passed through `--module` as trusted host code.

This transport remains independent of Codex, MCP, or any other agent protocol. The local MCP adapter delegates to the same engine and returns these typed response objects as structured content.
