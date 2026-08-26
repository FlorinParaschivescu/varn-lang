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

expression    = primary, { ".", name } ;
primary       = literal | slot | call | some | none | list | record-value ;
call          = identifier, "(", arguments?, ")" ;
some          = "some", "(", expression, ")" ;
none          = "none", "[", type, "]" ;
list          = "list", "[", type, "]", "(", arguments?, ")" ;
record-value  = "rec", "[", name, "]", "(", field-values?, ")" ;
field-values  = field-value, { ",", field-value } ;
field-value   = name, "=", expression ;
arguments     = expression, { ",", expression } ;
literal       = integer | float | string | "true" | "false" | "null" ;
name-list     = identifier, { ",", identifier } ;
slot          = "@", digit, { digit } ;
identifier    = letter, { letter | digit | "_" | "." } ;
name          = letter, { letter | digit | "_" } ;
type          = ( identifier | "null" | "list", "[", type, "]" ), { "?" } ;
```

Record and field names are identifiers without `.` (`VARN2007`), so they never collide with dotted module function names. Blocks are contextually terminated by `else` or `end`. Whitespace is allowed between tokens. `#` starts a line comment. String escapes include `\\n`, `\\r`, `\\t`, `\\"`, and `\\\\`.
