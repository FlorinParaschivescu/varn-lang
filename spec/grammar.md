# Varn v0.1 grammar

The implemented source grammar is shown in compact EBNF. Newlines terminate declarations and statements.

```ebnf
program       = newline*, ( directive | record )*, ( function | record )+, EOF ;
directive     = capability | budget ;
record        = "rec", name, "(", record-fields?, ")", newline ;
record-fields = record-field, { ",", record-field } ;
record-field  = name, ":", type ;
capability    = "cap", "[", name-list?, "]", newline ;
budget        = "budget", "[", "steps", "=", integer, "]", newline ;
function      = "fn", identifier, "(", parameters?, ")", "->", type,
                effects?, newline, statement*, "end", newline? ;
parameters    = parameter, { ",", parameter } ;
parameter     = name, ":", type ;
effects       = "!", "[", name-list?, "]" ;

statement     = let | variable | assignment | return | call | conditional | loop | each ;
let           = "let", name, ":", type, expression, newline ;
variable      = "var", name, ":", type, expression, newline ;
assignment    = "set", name, expression, newline ;
return        = "ret", expression, newline ;
conditional   = bool-if | optional-if | result-if ;
bool-if       = "if", expression, newline,
                statement*,
                [ "else", newline, statement* ],
                "end", newline ;
optional-if   = "if", "let", name, ":", type, expression, newline,
                statement*,
                [ "else", newline, statement* ],
                "end", newline ;
result-if     = "if", "ok", name, ":", type, expression, newline,
                statement*,
                [ "else", [ "err", name, ":", type ], newline, statement* ],
                "end", newline ;
loop          = "loop", name, ":", type,
                "from", integer, "to", integer, "max", integer, newline,
                statement*, "end", newline ;
each          = "each", name, ":", type, "in", expression,
                "max", integer, newline, statement*, "end", newline ;

expression    = equality ;
equality      = comparison, { ( "==" | "!=" ), comparison } ;
comparison    = additive, { ( "<" | ">" | "<=" | ">=" ), additive } ;
additive      = multiplicative, { ( "+" | "-" ), multiplicative } ;
multiplicative = unary, { ( "*" | "/" | "%" ), unary } ;
unary         = [ "-" ], postfix ;
postfix       = primary, { ".", name } ;
primary       = literal | reference | call | group | some | none | list | record-value | ok | err ;
group         = "(", expression, ")" ;
reference     = name, { ".", name } ;
call          = identifier, "(", arguments?, ")" ;
some          = "some", "(", expression, ")" ;
none          = "none", "[", type, "]" ;
list          = "list", "[", type, "]", "(", arguments?, ")" ;
ok            = "ok", "(", expression, ")" ;
err           = "err", "[", type, "]", "(", expression, ")" ;
record-value  = "rec", "[", name, "]", "(", field-values?, ")" ;
field-values  = field-value, { ",", field-value } ;
field-value   = name, "=", expression ;
arguments     = expression, { ",", expression } ;
literal       = integer | float | string | "true" | "false" | "null" ;
name-list     = identifier, { ",", identifier } ;
identifier    = letter, { letter | digit | "_" | "." } ;
name          = letter, { letter | digit | "_" } ;
type          = ( identifier | "null"
                | "list", "[", type, "]"
                | "result", "[", type, "]" ), { "?" } ;
```

`max`, `from`, `to`, and `in` are **contextual** keywords: they carry meaning only inside a `loop` or `each` header, and are ordinary names everywhere else. `max(3,9)` is a call, `rec Window(max:i64)` declares a field, and `each item:i64 in values max 3` still parses. `fn`, `let`, `var`, `set`, `ret`, `end`, `if`, `else`, `loop`, `each`, `cap`, `budget`, `rec`, `list`, `result`, `ok`, `err`, `some`, `none`, `true`, `false`, and `null` are reserved everywhere.

Binding, record, and field names are identifiers without `.` (`VARN2007`), so they never collide with dotted module function names. A `reference` and a `call` are told apart by the parenthesis: `total` reads a binding and `total()` calls a function. The lexer folds dots into identifiers so `io.print` stays one token, which makes `order.items` arrive as a single identifier; the parser splits it into a reference and one field access per segment. `@` is not part of the language: numeric slots were replaced by named bindings, and the character is still recognized only to report `VARN1004`. Every binary operator is left-associative and desugars to the module call of the same meaning, so `a + b` and the rejected `add(a,b)` would produce an identical tree. Unary `-` applies to a numeric literal only. Blocks are contextually terminated by `else` or `end`. Whitespace is allowed between tokens. `#` starts a line comment. String escapes include `\\n`, `\\r`, `\\t`, `\\"`, and `\\\\`.
