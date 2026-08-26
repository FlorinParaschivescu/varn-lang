# Varn AI tool adapter

The Varn adapter is a local Model Context Protocol (MCP) server over standard input/output. It exposes schema-versioned structured results without putting an MCP dependency in the lexer, parser, type checker, runtime, or module SDK.

The adapter uses the stable official C# MCP SDK and exposes three tools:

| Tool | Inputs | Behavior |
| --- | --- | --- |
| `varn_check` | `source` | Parse and statically validate without execution. |
| `varn_inspect` | `source` | Validate and return the deterministic canonical structure. |
| `varn_run` | `source`, `allowedCapabilities`, `maxSteps`, `maxOutputCharacters` | Validate and execute under an explicit host policy. |

All tools return the same schema-v1 response objects documented in [tooling.md](tooling.md) through MCP `structuredContent`.

## Security boundary

- Tools accept source text, not file paths, so the adapter does not gain ambient filesystem access.
- The host registers only `CoreModule` and `ConsoleModule`; it cannot load external assemblies.
- `allowedCapabilities` is required. An empty array explicitly grants none.
- `maxSteps` is required and must be between 1 and 1,000,000.
- `maxOutputCharacters` is required and must be between 1 and 1,000,000. Excess output is truncated and reported as `VARN5005`.
- Source is limited to 1,000,000 characters.
- Client cancellation is forwarded to the interpreter.
- MCP protocol logs go to standard error. Standard output is reserved for protocol messages.

Host-policy diagnostics use the source-independent span `0:0`:

| Code | Meaning |
| --- | --- |
| `VARN5001` | Missing source or source-size ceiling exceeded. |
| `VARN5002` | Missing, invalid, or unsupported capability grants. |
| `VARN5003` | Invalid step ceiling. |
| `VARN5004` | Invalid output ceiling. |
| `VARN5005` | Output exceeded the ceiling and was truncated. |

## Run locally

Build first, then start the stdio server from an MCP client:

```sh
dotnet build Varn.slnx
dotnet run --project src/Varn.ToolHost --no-build
```

Do not run the second command in an ordinary interactive terminal and type Varn source into it; stdin and stdout carry MCP JSON-RPC messages.

## Recommended agent workflow

1. Call `varn_check` with generated source.
2. Repair the source from stable diagnostic codes and spans until `success` is true.
3. Optionally call `varn_inspect` when canonical structure matters.
4. Call `varn_run` only when execution is necessary, with the smallest capability set and bounded resources.

The local stdio process is single-client and terminates when the client closes its input stream. A remote or multi-tenant transport is intentionally outside this milestone.
