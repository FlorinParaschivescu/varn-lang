# Varn v0.1 types

The bootstrap checker recognizes five program-visible types:

- `i64`: signed 64-bit integer;
- `f64`: IEEE 754 binary64 floating point;
- `bool`: `true` or `false`;
- `str`: UTF-16 host string in the bootstrap runtime;
- `null`: the single null value.

`any` exists only in module signatures, currently for functions such as `io.print`. Programs cannot declare `any` slots or parameters.

Assignments, arguments, and returns require exact types. Optional values, lists, records, and `Result` are planned but not specified yet.

The standard core module provides `add`, `sub`, `mul`, and `div` for `i64` and `f64`. `eq` and `lt` return `bool` for supported exact operand types.
