# Varn benchmark

This directory measures one question: **when a solution is wrong, how does each language fail?**

```sh
dotnet run --project bench/Varn.Bench
```

The run is deterministic and calls no model. It writes [results/results.md](results/results.md) and `results/results.json`, and exits non-zero if any solution grades differently from the intent recorded in `solutions/manifest.json`, so the harness checks itself.

## What is measured

Four small structured rules (`tasks/*.json`), each with a prompt, an input contract, a result contract, and cases chosen to sit on the traps real solutions fall into: an inclusive band boundary, an empty list divided by its own length, truncation toward zero on a negative mean, an absent optional, and exact string matching.

For every solution the harness classifies the outcome across all cases:

| Outcome | Meaning |
| --- | --- |
| `correct` | Every case matches. |
| `rejected` | Refused before execution. Varn's checker; Python's compiler. |
| `crashed` | Ran and aborted. |
| `silent_wrong` | Ran to completion and returned a plausible, wrong answer. |

`silent_wrong` is the metric that matters. A rejected program costs a repair cycle. A crashed program is visible in a log. A silently wrong program ships a bad discount to a customer.

## What the numbers show

On the eight defects written in **both** languages, Varn is strictly better on three, tied on five, and worse on none. It converts two silently-wrong Python outcomes and one crash into rejections before execution. Paired silent-wrong count: **Varn 4, Python 6**.

The three Varn catches are shape and type errors — a fractional value where the contract says whole, a dropped output field, an unchecked optional. The five ties are pure logic errors: the wrong percentage, a strict comparison on an inclusive boundary, equality where a prefix was meant, case-folding a value that should match exactly. **No type system catches those, and they are the majority of the paired defects.** Varn's advantage is real and bounded.

Varn costs about **1.36x** the approximate tokens of Python for the same correct behaviour, concentrated in `result` handling — `order-discount`, the most failure-heavy task, is 1.95x.

## What the numbers do not show

Read these before quoting anything above.

**No model was involved.** Every solution here is hand-written. This measures *mechanism* — which defects each language's checker can catch — not *frequency*, which is how often a model actually makes each defect. Frequency is the number that decides whether Varn is worth using, and it requires generating solutions with a real model under identical conditions. That is the missing half of this benchmark.

**The defect set is hand-picked by Varn's own author**, which is a bias in Varn's favour. It is mitigated, not removed, by including defects Varn does not catch and reporting them in the same table.

**"Not expressible" is a win only when the language genuinely prevents the mistake.** One row qualifies: `score-average/floor-division`. Varn has exactly one integer division semantics, so the floor-versus-truncate confusion cannot arise.

An earlier revision of this benchmark also showed `order-discount/case-insensitive` as not expressible. That was not a safety property, only a missing `str.to_lower`. The function was added and the defect is now written in both languages, where it is silently wrong in both. Treat any future "not expressible" row with the same suspicion: check whether the language prevents the mistake or merely lacks the vocabulary to make it.

**Python runs without a type checker.** That reflects how an agent actually executes generated Python — no mypy in the loop. Running mypy would be a fairer *language* comparison and a less realistic *deployment* comparison. Both framings are defensible; this one is stated rather than hidden.

**A numerically-equal but wrongly-typed answer counts as wrong.** `235.0` where the contract says a whole number is graded `silent_wrong`, because the host receives a value its declared type does not admit. A reader who disagrees can see the exact expected and actual values in `results.json`.

**Token counts use a deterministic proxy tokenizer**, not any model's. It splits identifier runs, number runs, and individual symbols. It compares two sources on one ruler; it does not predict a billing line.

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
