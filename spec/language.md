# Varn v0.1 language

This document describes the implemented bootstrap subset, not the full language roadmap.

## Program contract

A program contains zero or more capability declarations, exactly one positive step budget, and one or more functions. It must define `fn main()->i64` with no parameters.

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

Slots have numeric identities and are immutable in v0.1. A slot must be declared before use and cannot be declared twice in one scope. Every function body must end in `ret`, and its expression must exactly match the declared return type.

Calls use a single canonical form: `name(arg0,arg1)`. The checker resolves program functions first and then module functions by exact parameter types. There are no implicit conversions.

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

There is no implicit descending loop, unbounded loop, or `break` in v0.1. The runtime charges the loop statement, every iteration boundary, and every body operation to the program's step budget.

## Effects and budgets

An effectful function declares its effects after the return type: `fn main()->i64 ![console]`. Calling an effectful program or module function requires the caller to declare the same effect, including calls nested in conditions or loops.

`budget[steps=N]` sets the program's maximum instruction budget. The host supplies its own maximum. Execution uses the lower value and fails deterministically when it is exceeded.

`varn inspect` emits a deterministic compact structural projection. It is useful for tests and experiments but is not yet the final serialized canonical format.
