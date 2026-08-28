# Varn v0.1 language

This document describes the implemented bootstrap subset, not the full language roadmap.

## Program contract

A program contains zero or more capability declarations, exactly one positive step budget, zero or more record declarations, and one or more functions. It must define an entry point named `main`.

## Entry point

`main` takes at most one parameter and returns either `i64` or a declared record:

```varn
fn main(order:Order)->Settlement
```

The parameter, when present, is the program's **input contract**: its type must be a declared record, and the host supplies its value separately from the source. The return type is the program's **result contract**. `VARN3004` reports too many parameters, a non-record input, or a return type that is neither `i64` nor a declared record.

This is what makes a program reusable. The data never appears in the source, so one checked program runs unchanged across many inputs, with no string interpolation and no regeneration.

A successful `i64` result remains the process exit code. A successful record result exits `0` and is carried in the structured `returnValue` instead.

```varn
cap[console.write]
budget[steps=100]
```

Capability names, function names, effects, and types are case-sensitive and compared ordinally.

## Functions and bindings

```varn
fn sum(left:i64,right:i64)->i64
    let total:i64 left + right
    ret total
end
```

A binding is named. `let` declares an immutable binding, while `var` declares a mutable one and `set` explicitly replaces its value:

```varn
var total:i64 0
set total total + 1
```

A binding must be declared before use and cannot be declared twice while an existing binding of the same name is in scope. Parameters, `let` bindings, and loop iterators are immutable. An assignment target must be a visible mutable binding, and its expression must exactly match the declared type. Assignments in nested conditions and loops update a visible outer mutable binding; bindings declared inside those blocks do not escape. Every path through a function body must reach `ret`, and its expression must exactly match the declared return type. A body that ends in `ret` always qualifies; so does one whose every branch returns, which is why no unreachable trailing `ret` is needed. A conditional counts only when both arms exist and both return. A `loop` or `each` never counts, because it may run zero times. `VARN3009` reports a body that can finish without returning.

Mutation diagnostics preserve the existing rules: `VARN3005` reports duplicate declarations and `VARN3010` reports unknown or out-of-scope targets. `VARN3024` reports assignment to an immutable binding, and `VARN3025` reports an assignment type mismatch.

A binding name is an identifier without a dot (`VARN2007`), which is what keeps it distinct from a dotted module function name. A name followed by `(` is a call and a name on its own is a reference, so `total` reads a binding and `total()` calls a function. Numeric slots (`@0`) were the earlier form and are gone; the lexer still recognizes `@` only to report `VARN1004`, which names the replacement.

Calls use a single canonical form: `name(arg0,arg1)`. The checker resolves program functions first and then module functions by exact parameter types. There are no implicit conversions.

## Operators

Arithmetic and comparison are infix:

```varn
let discount:i64 total * percent / 100
let eligible:bool total >= 1000
```

Precedence runs, tightest first: field access, then unary `-` and `!`, then `*` `/` `%`, then `+` `-`, then `<` `>` `<=` `>=`, then `==` `!=`, then `&&`, then `||`. All the binary operators are left-associative and `( )` groups. Every operator desugars to the module call it always was, so the checker, the interpreter, the canonical projection, and the step budget see exactly what they saw before — an operator costs one step, like the call.

One concept gets one form, so every operator's call spelling is rejected: the prefix call form of `+`, `-`, `*`, `/`, `%`, `==`, `!=`, `<`, `>`, `<=`, `>=`, `&&`, `||`, and `!` each report `VARN2008` naming the operator to write. A leading `-` negates a numeric literal (`-5`, `-1.5`) and nothing else; `VARN2009` reports `-value` and names `0 - value`, because a typed zero would have to guess between `i64` and `f64`.

## Typed optional values

An optional type adds `?` to one supported scalar type. Construction is explicit: `some(expression)` contains a value and `none[type]` represents a typed absence.

```varn
let answer:i64? some(42)
let missing:i64? none[i64]
```

Optional construction does not convert values implicitly. For example, `some(true)` has type `bool?` and cannot initialize an `i64?` binding. The supported element types are `i64`, `f64`, `bool`, and `str`; `null?`, `any?`, and nested optionals are rejected with `VARN3028`.

`if let` is the only operation that extracts a contained value:

```varn
if let value:i64 answer
    ret value
else
    ret 0
end
```

The source expression must be optional (`VARN3026`) and the binding type must exactly match its element type (`VARN3027`). The binding is immutable, exists only in the present branch, and cannot escape. The absent branch never creates the binding. There is no unchecked optional access operation.

## Typed lists

Typed list construction states the homogeneous scalar element type explicitly, so empty lists do not require contextual inference:

```varn
let values:list[i64] list[i64](10,20,30)
let count:i64 list.length(values)
let second:i64? list.get(values,1)
```

Lists are immutable and contain at most 1,024 elements (`VARN3031`). Every literal element must exactly match the declared element type (`VARN3030`); supported types are `i64`, `f64`, `bool`, and `str` (`VARN3029`). `list.get` never traps for an invalid index: it returns a typed absence that must be handled with `if let`.

`each` traverses a list through an immutable element binding and requires a literal maximum:

```varn
var total:i64 0
each value:i64 in values max 3
    set total total + value
end
```

The source must be a list (`VARN3032`), the binding must match its element type (`VARN3033`), and `max` must be between 0 and 1,024 (`VARN3034`). The runtime rejects a list longer than the stated maximum with `VARN4006`; it never truncates traversal. Construction charges one step per element, and traversal charges every iteration boundary and body operation.

## Result values

`result[T]` carries either a success value of type `T` or a `str` failure message. `T` is a scalar or a declared record; lists, optionals, and nested results are not result value types (`VARN3045`).

```varn
fn rate(tier:str)->result[i64]
    if tier == "gold"
        ret ok(10)
    end
    ret err[i64](str.concat("unknown tier: ",tier))
end
```

`ok(expression)` infers its type from the expression. `err[T](expression)` states the success type it is standing in for, exactly as `none[T]` does, and its expression must be `str` (`VARN3046`).

`if ok` is the only operation that extracts either side:

```varn
if ok percent:i64 rate(order.customerTier)
    ret percent
else err reason:str
    ret str.length(reason)
end
```

The source must be a result (`VARN3047`) and the binding type must exactly match its success type (`VARN3048`). The `else err` clause is optional; a plain `else` runs the failure branch without binding the message. Both bindings are immutable, exist only in their own branch, and cannot escape. There is no unchecked extraction.

A result is for an **expected, in-domain failure**: an unknown tier, an unparsable field, a divisor that is data. It is not for defects. `div` and `mod` still trap on a zero divisor, because a zero literal divisor is a bug rather than an outcome; use `num.div` and `num.mod` when the divisor comes from input.

`main` may return `result[T]`. A program that returns a failure ran correctly, so the run reports `success` with no diagnostics and the failure appears in `returnValue`; the process exit code is `1`, distinguishing a rule that did not hold from a run that was rejected.

## Typed records

A record declaration is a program-level directive. It names a closed structure and lists its fields in one line:

```varn
rec Order(items:list[i64],tier:str)
```

Record names are ordinal and unique across the program. They must not shadow a built-in type name (`VARN3036`). Field names must be unique inside their record (`VARN3037`). A field type must be a *contained* type -- a scalar or another declared record -- or an optional or list of one (`VARN3038`). Nesting stops there: no lists of lists, no optional optionals, no results in fields.

A record that can reach itself through its fields, directly or through another record, describes a value of unbounded size and is rejected with `VARN3049`. Declaration order defines the record's field order, and that order is the only field order the runtime, the canonical projection, and JSON results ever use.

Construction names the record and sets every declared field exactly once:

```varn
let order:Order rec[Order](items=list[i64](1200,850,300),tier="gold")
```

The checker reports each fault exactly:

- `VARN3039` when a declared field is not set, naming the missing fields in declaration order;
- `VARN3040` when a field the record does not declare is set;
- `VARN3041` when a field is set more than once;
- `VARN3042` when a field value does not exactly match its declared type.

Field initializers may appear in any source order. The checker, interpreter, and canonical formatter all normalize them to declaration order, so two sources that differ only in field order produce the same canonical projection, the same result, and the same step count.

A field is read with a postfix `.name` on a record-valued expression:

```varn
let items:list[i64] order.items
let discount:i64 settle(order).discount
let city:str order.home.city
```

Access chains through nested records, and each step is one operation.

Field access is static. The target must be a record (`VARN3043`) and the field must be declared (`VARN3044`); there is no dynamic property lookup, no field enumeration, and no reflection over a record. Records are immutable: there is no field assignment form, and a whole-record `set` still requires a mutable binding.

Construction charges one step per field and field access charges one step, so the cost of a structured value is visible in the step budget.

## Conditions

Comparison is infix: `==`, `!=`, `<`, `>`, `<=`, and `>=`. So are the boolean operators `&&`, `||`, and prefix `!`. `&&` and `||` short-circuit, so the right operand runs only when the left does not already decide the answer, and a branch that must not run no longer needs a nested `if` to protect it.

```varn
if total >= 1000 && (order.customerTier == "gold" || str.starts_with(order.customerTier,"vip"))
```

An `if` condition must have type `bool`. Branch-local bindings do not escape their branch. `else` is optional, and a `ret` in the selected branch immediately returns from the containing function.

```varn
if count == 0
    ret 1
else
    ret 2
end
```

## Statically bounded loops

Loops use an immutable `i64` iterator and literal half-open bounds. The iterator exists only inside the loop body.

```varn
loop step:i64 from 0 to 3 max 3
    io.print(step)
end
```

This executes for `step` values `0`, `1`, and `2`. The checker requires:

- an `i64` iterator;
- `end >= start`;
- a nonnegative `max`;
- `max` exactly equal to the statically known `end - start` iteration count.

There is no implicit descending loop, unbounded loop, or `break` in v0.1. The runtime charges the loop statement, every iteration boundary, and every body operation—including mutable declarations and assignments—to the program's step budget.

## Effects and budgets

An effectful function declares its effects after the return type: `fn main()->i64 ![console]`. Calling an effectful program or module function requires the caller to declare the same effect, including calls nested in conditions or loops.

`budget[steps=N]` sets the program's maximum instruction budget. The host supplies its own maximum. Execution uses the lower value and fails deterministically when it is exceeded.

Record declarations appear in the canonical projection as `T[...]`, sorted ordinally by name with their declared field order preserved.

`varn inspect` emits a deterministic compact structural projection. It is useful for tests and experiments but is not yet the final serialized canonical format.
