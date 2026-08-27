# Varn benchmark

This directory measures one question: **when a solution is wrong, how does each language fail?**

```sh
dotnet run --project bench/Varn.Bench
```

The run is deterministic and calls no model. It writes [results/results.md](results/results.md) and `results/results.json`, and exits non-zero if any solution grades differently from the intent recorded in `solutions/manifest.json`, so the harness checks itself.

## What is measured

Six small rules (`tasks/*.json`), each with a prompt, an input contract, a result contract, and cases chosen to sit on the traps real solutions fall into.

Four take flat scalar input: an inclusive band boundary, an empty list divided by its own length, truncation toward zero on a negative mean, an absent optional, and exact string matching. Two take **structured** input — a list of records, a nested record, an optional record, and a list-valued output field — because that is the shape host data actually has, and it is where a declared contract carries information a JSON dictionary does not.

For every solution the harness classifies the outcome across all cases:

| Outcome | Meaning |
| --- | --- |
| `correct` | Every case matches. |
| `rejected` | Refused before execution. Varn's checker; Python's compiler. |
| `crashed` | Ran and aborted. |
| `silent_wrong` | Ran to completion and returned a plausible, wrong answer. |

`silent_wrong` is the metric that matters. A rejected program costs a repair cycle. A crashed program is visible in a log. A silently wrong program ships a bad discount to a customer.

## What the numbers show

On the sixteen defects written in **both** languages, Varn is strictly better on seven, tied on nine, and worse on none. It converts four silently-wrong Python outcomes and three crashes into rejections before execution. Paired silent-wrong count: **Varn 8, Python 12**.

The seven Varn catches are shape and type errors — a fractional value where the contract says whole, a dropped output field, a field name the record does not declare, a nested record returned where a string was declared, an unchecked optional. The nine ties are pure logic errors: the wrong percentage, a strict comparison on an inclusive boundary, equality where a prefix was meant, counting lines where units were meant, reporting the last offending item instead of the first, case-folding a value that should match exactly. **No type system catches those, and they are the majority of the paired defects.** Varn's advantage is real and bounded.

The split by task shape is why the structured tasks were added. On the eight paired defects in the flat tasks, Varn is better on three and tied on five. On the eight paired defects in the structured tasks — `invoice-lines` and `contact-routing` — it is better on four and tied on four. Structure is where a contract has something to say: a misnamed nested field, a missing list-valued output field, and an unguarded optional record are all refused before execution, and two of those three only crash in Python rather than being caught.

Varn costs about **1.17x** the approximate tokens of Python for the same correct behaviour, concentrated in `result` handling — `order-discount`, the most failure-heavy task, is 1.71x. Structure narrows the gap: `invoice-lines` is 1.21x and `contact-routing` is **0.95x**, where Varn is cheaper, because `customer.primary.address` costs fewer proxy tokens than `customer["primary"]["address"]`.

That ratio was 1.36x when the solutions used numeric slots and carried unreachable trailing returns. Two changes moved it, and they are worth separating. On the four flat tasks, replacing `@0` with a name saved 46 proxy tokens and deleting `ret err[T]("unreachable")` lines the checker no longer requires saved 20. Read the first number with suspicion: this tokenizer charges `@0` as two tokens (a symbol and a number) and any name as one, which flatters names. A real tokenizer would not agree, and the case for named bindings was never token count — it is that a name is decided once at its binding, while a slot number is state the generator carries through the whole function.

## What the numbers do not show

Read these before quoting anything above.

**No model was involved.** Every solution here is hand-written. This measures *mechanism* — which defects each language's checker can catch — not *frequency*, which is how often a model actually makes each defect. Frequency is the number that decides whether Varn is worth using, and it requires generating solutions with a real model under identical conditions. That is the missing half of this benchmark.

**The defect set is hand-picked by Varn's own author**, which is a bias in Varn's favour. It is mitigated, not removed, by including defects Varn does not catch and reporting them in the same table. The structured tasks were added by the same author and carry the same bias: they were chosen because structure is where Varn should do well, and nothing here shows how much real work has that shape.

**"Not expressible" is a win only when the language genuinely prevents the mistake.** One row qualifies: `score-average/floor-division`. Varn has exactly one integer division semantics, so the floor-versus-truncate confusion cannot arise.

An earlier revision of this benchmark also showed `order-discount/case-insensitive` as not expressible. That was not a safety property, only a missing `str.to_lower`. The function was added and the defect is now written in both languages, where it is silently wrong in both. Treat any future "not expressible" row with the same suspicion: check whether the language prevents the mistake or merely lacks the vocabulary to make it.

**Python runs without a type checker.** That reflects how an agent actually executes generated Python — no mypy in the loop. Running mypy would be a fairer *language* comparison and a less realistic *deployment* comparison. Both framings are defensible; this one is stated rather than hidden.

**A numerically-equal but wrongly-typed answer counts as wrong.** `235.0` where the contract says a whole number is graded `silent_wrong`, because the host receives a value its declared type does not admit. A reader who disagrees can see the exact expected and actual values in `results.json`.

**Token counts use a deterministic proxy tokenizer**, not any model's. It splits identifier runs, number runs, and individual symbols. It compares two sources on one ruler; it does not predict a billing line. It is also not neutral between syntaxes: it charges one token per identifier however long the name, so it rewards long names and punishes symbol-heavy forms. Any comparison it reports between two *versions of Varn* is weaker evidence than the same comparison against Python.

## Adding the missing half

To measure frequency rather than mechanism, generate solutions instead of reading them from disk:

1. For each task, send `prompt` plus the input and result contracts to a model.
2. Write the response to `solutions/<language>/<task>/<run-id>.<ext>` and add a manifest row.
3. Re-run the harness. Classification, grading, and token counting already work unchanged.
4. Repeat across models and temperatures, and report the distribution rather than a single run.

The repair loop is the other half: feed Varn's diagnostics back and count cycles to `correct`, which is what "tokens to verified-correct" in `ROADMAP.md` means. Varn should lose the first-attempt comparison — models have seen no Varn — and the open question is whether the repair loop closes that gap faster than Python's silent failures cost.

## Layout

```text
bench/
  tasks/                 task definitions, cases, and the traps each case targets
  solutions/
    manifest.json        every solution, its language, and the outcome it was written to have
    varn/<task>/*.varn
    python/<task>/*.py
  results/               generated report, committed so changes to the conclusions are reviewable
  Varn.Bench/            the harness
```
