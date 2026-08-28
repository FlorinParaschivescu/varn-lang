# Varn v0.1 types

The bootstrap checker recognizes five program-visible types:

- `i64`: signed 64-bit integer;
- `f64`: IEEE 754 binary64 floating point;
- `bool`: `true` or `false`;
- `str`: UTF-16 host string in the bootstrap runtime;
- `null`: the single null value.

`any` exists only in module signatures, currently for functions such as `io.print`. Programs cannot declare `any` bindings or parameters.

Appending `?` creates an optional over a scalar or a declared record. `some(value)` produces a present optional and `none[type]` produces an absent optional with the same exact type. `null?`, `any?`, and nested optionals are intentionally unsupported. Optional values can be extracted only through an `if let` binding.

`result[T]` carries either a success value of type `T` or a `str` failure message, where `T` is a scalar or a declared record. `ok(value)` and `err[T](message)` construct it, and `if ok` is the only way to read either side. Optionals represent absence; results represent failure with a reason.

`list[T]` is an immutable homogeneous list whose element type is a scalar or a declared record. Construction is explicit as `list[T](value0,value1)`, including `list[T]()` for an empty list. Lists contain at most 1,024 elements. Nesting stops at one level: a list of lists, an optional list, and a list of optionals are all unsupported.

`list.length(values)` returns `i64`. `list.get(values,index)` returns `T?`; negative and out-of-range indexes produce `none[T]`. `each` traverses elements only when the runtime list length is at most its explicit `max` ceiling.

A `rec` declaration introduces a closed named record type. Its fields are ordered by declaration, unique, and typed as a contained type: a scalar or a declared record, or an optional or list of one. Nesting stops there, so a list of lists, an optional optional, and a result in a field are all rejected. A record that can reach itself through its fields is rejected with `VARN3049`.

`rec[Name](field=value,...)` constructs a record and requires every declared field exactly once. `value.field` reads one declared field and has that field's declared type. Records are immutable, are compared by nothing (there is no `eq` overload for them), and expose no dynamic property access.

Mutable declarations, assignments, arguments, and returns require exact types. There are no implicit conversions. `Result` is planned but not specified yet.

## Standard operations

The core module provides every operation below. Each is total, pure, deterministic, capability-free, and exactly typed: operands must match a listed signature exactly, because there are no implicit conversions.

| Group | Operations | Operand types | Result |
| --- | --- | --- | --- |
| Arithmetic | `+`, `-`, `*`, `/` | `i64`, `f64` | same as operands |
| Arithmetic | `%` | `i64` | `i64` |
| Arithmetic | `min`, `max` | `i64`, `f64` | same as operands |
| Arithmetic | `abs` | `i64`, `f64` | same as operand |
| Boolean | `&&`, `\|\|` | `bool` | `bool` |
| Boolean | `!` | `bool` (one operand) | `bool` |
| Equality | `==`, `!=` | `i64`, `f64`, `bool`, `str` | `bool` |
| Ordering | `<`, `>`, `<=`, `>=` | `i64`, `f64`, `str` | `bool` |
| String | `str.length` | `str` | `i64` |
| String | `str.concat` | `str`, `str` | `str` |
| String | `str.from_i64`, `str.from_f64`, `str.from_bool` | `i64` / `f64` / `bool` | `str` |
| String | `str.to_lower`, `str.to_upper` | `str` | `str` |
| String | `str.contains`, `str.starts_with`, `str.ends_with` | `str`, `str` | `bool` |
| List | `list.length` | `list[T]` | `i64` |
| List | `list.get` | `list[T]`, `i64` | `T?` |
| List | `list.append` | `list[T]`, `T` | `list[T]` |
| List | `list.contains` | `list[T]`, `T` | `bool` (scalar `T` only) |

Arithmetic and comparison are infix and desugar to those same operations, so an operator costs exactly what the call cost: one step, and the identical canonical projection. The call spelling is rejected (`VARN2008`), because one concept gets one form.

A leading `-` negates a **numeric literal** and nothing else, so `-5` is a literal and `0 - value` is how a value is negated (`VARN2009`). This avoids a typed zero that would have to guess between `i64` and `f64`.

`&&` and `||` **short-circuit**: the right operand is evaluated only when the left does not already decide the answer. `!` desugars to the `not` call it replaces, but `&&` and `||` cannot, because a call would evaluate both operands; they are their own node and their own canonical projection.

This makes a step count depend on the data. That is not new — `each` over a host list already charges per element — and it does not weaken determinism: the same input charges the same steps, and the budget is still a ceiling the run cannot exceed. What it does mean is that a step count is not readable from program shape alone. Both operands of `&&` and `||` must be `bool` (`VARN3050`), and a single `&` or `|` reports `VARN1005`, since there is no bitwise arithmetic to confuse it with.

String comparison and search are ordinal and case-sensitive, never culture-sensitive. `str.to_lower` and `str.to_upper` use invariant casing, so their result never depends on the host's locale. `str.from_i64` and `str.from_f64` format invariantly, `str.from_f64` round-trips, and `str.from_bool` yields `true` or `false`. These are what put a value into a failure message: `err[T](str.concat("over limit of ",str.from_i64(order.limit)))`. `str.length` counts UTF-16 code units in the bootstrap runtime.

`f64` comparison follows IEEE 754 directly: every `eq`, `lt`, `gt`, `lte`, and `gte` involving NaN is `false`, and `ne` is `true`. `i64` and `str` compare by total order.

`list.contains` charges one step per element examined and is defined for scalar elements only. `list.append` returns a **new** list with one element added; the original is unchanged, and exceeding the 1,024-element ceiling fails with `VARN4007`. Together with `each`, that is how a program builds a list.

These operations return `result[T]` because they can fail in-domain:

| Operation | Signature | Failure messages |
| --- | --- | --- |
| `num.div`, `num.mod` | `i64`, `i64` -> `result[i64]` | `divide by zero`, `outside the i64 range` |
| `num.to_i64` | `f64` -> `result[i64]` | `not a finite number`, `not a whole number`, `outside the i64 range` |
| `str.to_i64` | `str` -> `result[i64]` | `not an i64` |
| `str.to_f64` | `str` -> `result[f64]` | `not an f64` |

`num.to_f64` converts `i64` to `f64` and is total, so it returns `f64` directly. Values beyond 2^53 lose precision without failing, which is ordinary IEEE 754 behaviour.

Total `div` and `mod` remain, and still trap on a zero divisor as the module failure `VARN4003`. That is deliberate: a zero literal divisor is a defect, and a defect should abort rather than be silently handled. Use the `num.` forms whenever the divisor is data.
