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

Mutable declarations, assignments, arguments, and returns require exact types. There are no implicit conversions. Records and `Result` are planned but not specified yet.

The standard core module provides `add`, `sub`, `mul`, and `div` for `i64` and `f64`. `eq` and `lt` return `bool` for supported exact operand types.
