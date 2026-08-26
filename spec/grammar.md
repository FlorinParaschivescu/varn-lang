# Varn v0.1 grammar

The implemented source grammar is shown in compact EBNF. Newlines terminate declarations and statements.

```ebnf
program       = newline*, directive*, function+, EOF ;
directive     = capability | budget ;
capability    = "cap", "[", name-list?, "]", newline ;
budget        = "budget", "[", "steps", "=", integer, "]", newline ;
function      = "fn", identifier, "(", parameters?, ")", "->", type,
                effects?, newline, statement*, "end", newline? ;
parameters    = parameter, { ",", parameter } ;
parameter     = slot, ":", type ;
effects       = "!", "[", name-list?, "]" ;

statement     = let | variable | assignment | return | call | conditional | loop | each ;
let           = "let", slot, ":", type, expression, newline ;
variable      = "var", slot, ":", type, expression, newline ;
assignment    = "set", slot, expression, newline ;
return        = "ret", expression, newline ;
conditional   = bool-if | optional-if ;
bool-if       = "if", expression, newline,
                statement*,
                [ "else", newline, statement* ],
                "end", newline ;
optional-if   = "if", "let", slot, ":", type, expression, newline,
                statement*,
                [ "else", newline, statement* ],
                "end", newline ;
loop          = "loop", slot, ":", type,
                "from", integer, "to", integer, "max", integer, newline,
                statement*, "end", newline ;
each          = "each", slot, ":", type, "in", expression,
                "max", integer, newline, statement*, "end", newline ;

expression    = literal | slot | call | some | none | list ;
call          = identifier, "(", arguments?, ")" ;
some          = "some", "(", expression, ")" ;
none          = "none", "[", type, "]" ;
list          = "list", "[", type, "]", "(", arguments?, ")" ;
arguments     = expression, { ",", expression } ;
literal       = integer | float | string | "true" | "false" | "null" ;
name-list     = identifier, { ",", identifier } ;
slot          = "@", digit, { digit } ;
identifier    = letter, { letter | digit | "_" | "." } ;
type          = ( identifier | "null" | "list", "[", type, "]" ), { "?" } ;
```

Blocks are contextually terminated by `else` or `end`. Whitespace is allowed between tokens. `#` starts a line comment. String escapes include `\\n`, `\\r`, `\\t`, `\\"`, and `\\\\`.
