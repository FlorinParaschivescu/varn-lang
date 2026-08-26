# Capabilities

Varn separates capability declaration from host authorization.

1. A module function declares the capability it requires.
2. The Varn program lists that capability in `cap[...]`.
3. The checker rejects a call when the program omitted it.
4. The host independently grants a subset when execution begins.
5. The runtime rejects a call when the host omitted the grant.

For `io.print`, the program needs `cap[console.write]` and the host needs `--allow console.write`. The program cannot grant itself access.

v0.1 capabilities are exact string identifiers. Structured restrictions such as allowed domains, filesystem roots, byte limits, or HTTP methods are deliberately deferred. A future network module should fail closed until those restrictions can be represented and enforced by its host adapter.
