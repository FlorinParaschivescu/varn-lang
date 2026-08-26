# Effects

Effects describe observable behavior in a function contract. They are statically checked and propagate through calls.

```varn
fn main()->i64 ![console]
    io.print(30)
    ret 0
end
```

`io.print` is registered with the `console` effect. Omitting `![console]` from the caller is a validation error.

Effects answer **what kind of behavior can occur**. Capabilities answer **which resource is requested and permitted**. An effect declaration does not grant a capability.

The current model uses stable string identifiers. Later versions may add structured effect rows or parameters, but implicit effects will remain invalid.
