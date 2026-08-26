# Varn v0.1 types

The bootstrap checker recognizes five program-visible types:

- `i64`: signed 64-bit integer;
- `f64`: IEEE 754 binary64 floating point;
- `bool`: `true` or `false`;
- `str`: UTF-16 host string in the bootstrap runtime;
- `null`: the single null value.

`any` exists only in module signatures, currently for functions such as `io.print`. Programs cannot declare `any` slots or parameters.

Appending `?` creates an optional over `i64`, `f64`, `bool`, or `str`. `some(value)` produces a present optional and `none[type]` produces an absent optional with the same exact type. `null?`, `any?`, and nested optionals are intentionally unsupported. Optional values can be extracted only through an `if let` binding.

`list[T]` is an immutable homogeneous list whose element type is one of `i64`, `f64`, `bool`, or `str`. Construction is explicit as `list[T](value0,value1)`, including `list[T]()` for an empty list. Lists contain at most 1,024 elements. Nested lists and optional list elements are intentionally unsupported in this slice.

`list.length(values)` returns `i64`. `list.get(values,index)` returns `T?`; negative and out-of-range indexes produce `none[T]`. `each` traverses elements only when the runtime list length is at most its explicit `max` ceiling.

A `rec` declaration introduces a closed named record type. Its fields are ordered by declaration, unique, and typed as a scalar, an optional scalar, or a list of scalars. A record type name is not a scalar: it cannot be an optional element type (`VARN3028`), a list element type (`VARN3029`), or another record's field type (`VARN3038`).

`rec[Name](field=value,...)` constructs a record and requires every declared field exactly once. `value.field` reads one declared field and has that field's declared type. Records are immutable, are compared by nothing (there is no `eq` overload for them), and expose no dynamic property access.

Mutable declarations, assignments, arguments, and returns require exact types. There are no implicit conversions. `Result` is planned but not specified yet.

## Standard operations

The core module provides every operation below. Each is total, pure, deterministic, capability-free, and exactly typed: operands must match a listed signature exactly, because there are no implicit conversions.

| Group | Operations | Operand types | Result |
| --- | --- | --- | --- |
| Arithmetic | `add`, `sub`, `mul`, `div` | `i64`, `f64` | same as operands |
| Arithmetic | `mod` | `i64` | `i64` |
| Arithmetic | `min`, `max` | `i64`, `f64` | same as operands |
| Arithmetic | `abs` | `i64`, `f64` | same as operand |
| Boolean | `and`, `or` | `bool` | `bool` |
| Boolean | `not` | `bool` (one operand) | `bool` |
| Equality | `eq`, `ne` | `i64`, `f64`, `bool`, `str` | `bool` |
| Ordering | `lt`, `gt`, `lte`, `gte` | `i64`, `f64`, `str` | `bool` |
| String | `str.length` | `str` | `i64` |
| String | `str.concat` | `str`, `str` | `str` |
| String | `str.contains`, `str.starts_with`, `str.ends_with` | `str`, `str` | `bool` |
| List | `list.length` | `list[T]` | `i64` |
| List | `list.get` | `list[T]`, `i64` | `T?` |
| List | `list.contains` | `list[T]`, `T` | `bool` |

`and` and `or` are ordinary calls, so **both operands are always evaluated**. There is no short-circuiting: a call charges the same steps regardless of operand values, which keeps step accounting a function of program shape rather than data. Write `if` when a branch must not be evaluated.

String comparison and search are ordinal and case-sensitive, never culture-sensitive. `str.length` counts UTF-16 code units in the bootstrap runtime.

`f64` comparison follows IEEE 754 directly: every `eq`, `lt`, `gt`, `lte`, and `gte` involving NaN is `false`, and `ne` is `true`. `i64` and `str` compare by total order.

`list.contains` charges one step per element examined.

Deliberately absent until `Result` lands: numeric conversion, string-to-number parsing, and any other operation with an expected failure. Integer `div` and `mod` by zero currently surface as the runtime module failure `VARN4003`; `Result` will make that an explicit value.
