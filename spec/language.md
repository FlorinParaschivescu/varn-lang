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

Slots have numeric identities and are immutable in v0.1. A slot must be declared before use and cannot be declared twice in one function. Every function body must end in `ret`, and its expression must exactly match the declared return type.

Calls use a single canonical form: `name(arg0,arg1)`. The checker resolves program functions first and then module functions by exact parameter types. There are no implicit conversions.

## Effects and budgets

An effectful function declares its effects after the return type: `fn main()->i64 ![console]`. Calling an effectful program or module function requires the caller to declare the same effect.

`budget[steps=N]` sets the program's maximum instruction budget. The host supplies its own maximum. Execution uses the lower value and fails deterministically when it is exceeded.

`varn inspect` emits a deterministic compact structural projection. It is useful for tests and experiments but is not yet the final serialized canonical format.
