# Varn v0.1 language

This document describes the implemented bootstrap subset, not the full language roadmap.

## Program contract

A program contains zero or more capability declarations, exactly one positive step budget, zero or more record declarations, and one or more functions. It must define `fn main()->i64` with no parameters.

```varn
cap[console.write]
budget[steps=100]
```

Capability names, function names, effects, and types are case-sensitive and compared ordinally.

## Functions and slots

```varn
fn sum(@0:i64,@1:i64)->i64
    let @2:i64 add(@0,@1)
    ret @2
end
```

Slots have numeric identities. `let` declares an immutable slot, while `var` declares a mutable slot and `set` explicitly replaces its value:

```varn
var @0:i64 0
set @0 add(@0,1)
```

A slot must be declared before use and cannot be declared twice while an existing slot with the same numeric identity is in scope. Parameters, `let` slots, and loop iterators are immutable. An assignment target must be a visible mutable slot, and its expression must exactly match the declared type. Assignments in nested conditions and loops update a visible outer mutable slot; slots declared inside those blocks do not escape. Every function body must end in `ret`, and its expression must exactly match the declared return type.

Mutation diagnostics preserve the existing slot rules: `VARN3005` reports duplicate declarations and `VARN3010` reports unknown or out-of-scope targets. `VARN3024` reports assignment to an immutable slot, and `VARN3025` reports an assignment type mismatch.

Calls use a single canonical form: `name(arg0,arg1)`. The checker resolves program functions first and then module functions by exact parameter types. There are no implicit conversions.

## Typed optional values

An optional type adds `?` to one supported scalar type. Construction is explicit: `some(expression)` contains a value and `none[type]` represents a typed absence.

```varn
let @0:i64? some(42)
let @1:i64? none[i64]
```

Optional construction does not convert values implicitly. For example, `some(true)` has type `bool?` and cannot initialize an `i64?` slot. The supported element types are `i64`, `f64`, `bool`, and `str`; `null?`, `any?`, and nested optionals are rejected with `VARN3028`.

`if let` is the only operation that extracts a contained value:

```varn
if let @2:i64 @0
    ret @2
else
    ret 0
end
```

The source expression must be optional (`VARN3026`) and the binding type must exactly match its element type (`VARN3027`). The binding is immutable, exists only in the present branch, and cannot escape. The absent branch never creates the binding. There is no unchecked optional access operation.

## Typed lists

Typed list construction states the homogeneous scalar element type explicitly, so empty lists do not require contextual inference:

```varn
let @0:list[i64] list[i64](10,20,30)
let @1:i64 list.length(@0)
let @2:i64? list.get(@0,1)
```

Lists are immutable and contain at most 1,024 elements (`VARN3031`). Every literal element must exactly match the declared element type (`VARN3030`); supported types are `i64`, `f64`, `bool`, and `str` (`VARN3029`). `list.get` never traps for an invalid index: it returns a typed absence that must be handled with `if let`.

`each` traverses a list through an immutable element binding and requires a literal maximum:

```varn
var @1:i64 0
each @2:i64 in @0 max 3
    set @1 add(@1,@2)
end
```

The source must be a list (`VARN3032`), the binding must match its element type (`VARN3033`), and `max` must be between 0 and 1,024 (`VARN3034`). The runtime rejects a list longer than the stated maximum with `VARN4006`; it never truncates traversal. Construction charges one step per element, and traversal charges every iteration boundary and body operation.

## Typed records

A record declaration is a program-level directive. It names a closed structure and lists its fields in one line:

```varn
rec Order(items:list[i64],tier:str)
```

Record names are ordinal and unique across the program. They must not shadow a built-in type name (`VARN3036`). Field names must be unique inside their record (`VARN3037`). A field type must be a scalar, an optional scalar, or a list of scalars (`VARN3038`); nested records are intentionally unsupported in this slice. Declaration order defines the record's field order, and that order is the only field order the runtime, the canonical projection, and JSON results ever use.

Construction names the record and sets every declared field exactly once:

```varn
let @0:Order rec[Order](items=list[i64](1200,850,300),tier="gold")
```

The checker reports each fault exactly:

- `VARN3039` when a declared field is not set, naming the missing fields in declaration order;
- `VARN3040` when a field the record does not declare is set;
- `VARN3041` when a field is set more than once;
- `VARN3042` when a field value does not exactly match its declared type.

Field initializers may appear in any source order. The checker, interpreter, and canonical formatter all normalize them to declaration order, so two sources that differ only in field order produce the same canonical projection, the same result, and the same step count.

A field is read with a postfix `.name` on a record-valued expression:

```varn
let @1:list[i64] @0.items
let @2:i64 settle(@0).discount
```

Field access is static. The target must be a record (`VARN3043`) and the field must be declared (`VARN3044`); there is no dynamic property lookup, no field enumeration, and no reflection over a record. Records are immutable: there is no field assignment form, and a whole-record `set` still requires a mutable slot.

Construction charges one step per field and field access charges one step, so the cost of a structured value is visible in the step budget.

## Conditions

An `if` condition must have type `bool`. Branch-local slots do not escape their branch. `else` is optional, and a `ret` in the selected branch immediately returns from the containing function.

```varn
if eq(@0,0)
    ret 1
else
    ret 2
end
```

## Statically bounded loops

Loops use an immutable `i64` iterator and literal half-open bounds. The iterator exists only inside the loop body.

```varn
loop @0:i64 from 0 to 3 max 3
    io.print(@0)
end
```

This executes for `@0` values `0`, `1`, and `2`. The checker requires:

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
