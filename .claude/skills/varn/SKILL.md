---
name: varn
description: Write, check, and run Varn — a small deterministic language for business rules an AI generates and a host executes in-process, with contracts, metered steps, and explicit capabilities. Use when a rule must be verified before it runs, run unchanged over many inputs, or executed without a sandbox per call.
---

# Varn

A rule is a program. The data is not: `main` takes one record supplied by the host, so one checked program runs unchanged over every input, with no regeneration and no string interpolation.

Check before running. The checker refuses whole classes of defect — a missing output field, a field name the record does not declare, an unguarded optional — before any code executes, which is cheaper than discovering them in a log.

## The whole language

```varn
cap[console.write]
budget[steps=400]

rec Order(items:list[i64],tier:str,note:str?)
rec Settlement(total:i64,discount:i64)

fn rate(tier:str)->result[i64]
    if tier == "gold"
        ret ok(10)
    end
    ret err(str.concat("unknown tier: ",tier))
end

fn main(order:Order)->result[Settlement] ![console]
    var total:i64 0
    each item:i64 in order.items max 32
        set total total + item
    end
    if let note:str order.note
        io.print(note)
    end
    if ok percent:i64 rate(order.tier)
        if ok discount:i64 num.div(total * percent,100)
            ret ok(rec[Settlement](total=total,discount=discount))
        else err reason:str
            ret err(reason)
        end
    else err reason:str
        ret err(reason)
    end
end
```

## Rules that are not obvious

**Shape.** A program is `cap[...]` (optional), exactly one `budget[steps=N]`, `rec` declarations, then functions. It must define `main`. Statements are newline-terminated; blocks end with `end`; `#` starts a comment.

**The entry point.** `main` takes at most one parameter, which must be a declared record, and returns `i64`, a declared record, or a `result` of either. Never write the data into the source.

**Bindings.** `let` is immutable, `var` is mutable, `set name value` assigns. Every binding is named and typed. A binding cannot be redeclared while one of the same name is in scope, and bindings inside a branch or loop do not escape it.

**Returns.** Every path must reach `ret`. A body whose every branch returns qualifies, so no unreachable trailing `ret` is needed. A `loop` or `each` never counts, because it may run zero times.

**Types.** `i64`, `f64`, `bool`, `str`; `T?` optional; `list[T]`; `result[T]`; declared records. No implicit conversions anywhere — `1` and `1.0` are different types. A record field, list element, and optional may each hold a declared record, and nesting stops there.

**Optionals and results are the only way in.** `if let name:T optional` binds a present value; `if ok name:T result ... else err reason:str` binds either side. There is no unchecked access to either, and no way to forget the absent case.

**Records** are closed and immutable. `rec[Name](field=value,...)` must set every declared field exactly once, `value.field` reads one, and there is no field assignment and no dynamic property access.

**Lists** are immutable and hold at most 1,024 elements. `each name:T in values max N` needs a literal ceiling and fails at runtime if the list is longer. `list.append` returns a new list, so `each` plus a `var` is how a program builds one.

**Inferred type arguments.** `none`, `list(...)`, and `err(...)` take their type from the surrounding `let`, `var`, `set` target, `ret`, or record field; a list also takes it from its first element. Writing one the context already supplies is rejected. A call argument supplies nothing, so assign to a declared binding first.

**Effects and capabilities.** An effectful function declares `![console]` after its return type, and every caller must declare it too. `cap[console.write]` states what the program wants; the host grants it separately, and a declaration alone grants nothing.

**Budget.** `budget[steps=N]` is a ceiling on executed operations. The host may impose a lower one. Exceeding it fails deterministically.

## Operators

Tightest first: field access; unary `-` and `!`; `*` `/` `%`; `+` `-`; `<` `>` `<=` `>=`; `==` `!=`; `&&`; `||`. Binary operators are left-associative and `( )` groups.

`&&` and `||` short-circuit, so `count != 0 && total / count > 5` is safe on an empty list. Unary `-` applies to a numeric literal only; write `0 - value` to negate a value.

There is no call spelling for an operator: `add(a,b)` is rejected in favour of `a + b`.

## Every callable function

`min` `max` `abs` on `i64`/`f64`. `str.length` `str.concat` `str.contains` `str.starts_with` `str.ends_with` `str.to_lower` `str.to_upper` `str.from_i64` `str.from_f64` `str.from_bool`. `list.length` `list.get` `list.append` `list.contains`. `io.print` (needs `![console]`).

Returning `result[T]` because they can fail on data: `num.div` `num.mod` `num.to_i64` `str.to_i64` `str.to_f64`. `num.to_f64` is total.

Nothing else exists — do not invent a function. String comparison is ordinal and case-sensitive. `list.get` returns `T?` rather than trapping.

**`/` and `%` trap on a zero divisor; `num.div` and `num.mod` return a `result` instead.** Use the operator only when the divisor is a literal, and the `num.` form whenever it comes from data.

## Workflow

```bash
dotnet run --project src/Varn.Cli -- check rule.varn --json
```

```bash
dotnet run --project src/Varn.Cli -- run rule.varn --input data.json --json
```

Where the MCP host is connected, `varn_check`, `varn_inspect`, and `varn_run` do the same without leaving the session. Every run needs explicit `allowedCapabilities` and `maxSteps`.

1. Write the rule. Declare the records first: the input contract is the shape the host will send.
2. `check` it. `contract.input` in the response is the JSON shape to supply.
3. If it is rejected, **edit the lines the diagnostics name.** Do not regenerate the program — each diagnostic carries a code, a message, and a line, and most name the exact fix.
4. `run` it with the input. A failed `result` is a successful run: the rule executed and did not hold, so `success` is true and the failure is in `returnValue`.

## When the checker rejects

| Code | Means | Do |
| --- | --- | --- |
| `VARN1004` | `@0` used as a binding | Name the binding |
| `VARN2008` | Call spelling of an operator | Use the operator it names |
| `VARN2009` | `-value` on a non-literal | Write `0 - value` |
| `VARN3005` `VARN3010` | Binding redeclared, or not in scope | Rename, or move the declaration out of the branch |
| `VARN3006` `VARN3025` | Type mismatch on a declaration or assignment | Convert explicitly; there are no implicit conversions |
| `VARN3009` | A path reaches the end without `ret` | Return from every branch |
| `VARN3011` `VARN3012` | Unknown function, or no overload for those types | Use one from the list above, with exact types |
| `VARN3039` | Record construction is missing fields | Set every declared field |
| `VARN3043` `VARN3044` | Field read on a non-record, or an undeclared field | Check the `rec` declaration; bind an optional with `if let` first |
| `VARN3046` | A failure value that is not `str` | `str.from_i64` the value into the message |
| `VARN3051` | Nothing determines a type argument | Assign to a declared binding first |
| `VARN3052` | A type argument the context already supplies | Delete it |
| `VARN4002` | The host did not grant a declared capability | Ask the host to allow it; `cap[...]` alone grants nothing |
| `VARN4003` | `/` or `%` by zero at runtime | Use `num.div` or `num.mod` and handle the failure |
| `VARN4005` | The step budget was exhausted | Raise `budget[steps=N]`, or do less work per run |
| `VARN4006` | A list was longer than an `each` ceiling | Raise the `max`, which must stay a literal |
| `VARN6000`-`VARN6010` | The input JSON does not match the contract | Fix the JSON; the code names the field |

## Worked examples

Runnable, each with its input JSON, in `examples/`:

- `rule-over-a-record.varn` — one record in, one record out.
- `fold-over-line-items.varn` — a fold over a list of records that also builds a list.
- `checked-optional.varn` — an absent value that must be checked before use.
- `failure-as-a-value.varn` — an expected failure carried as `result`.
