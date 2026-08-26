# Module contract

Modules are the intended route from Varn code to host integrations. The core runtime knows how to register and call typed functions; it does not need to know about HTTP, databases, browsers, or vendor SDKs.

## Implement a module

Create a .NET 10 class library, reference `Varn.ModuleSdk`, and export a public `IVarnModule` implementation with a public parameterless constructor:

```csharp
using Varn.ModuleSdk;
using Varn.Syntax;

public sealed class TextModule : IVarnModule
{
    public string Name => "example.text";

    public void Register(VarnModuleBuilder builder)
    {
        builder.Function(
            new VarnFunctionSignature("text.length", [VarnType.String], VarnType.I64),
            static (_, args, _) => ValueTask.FromResult(
                VarnValue.From((long)((string)args[0].Value!).Length)));
    }
}
```

Effectful functions declare both an effect and a capability:

```csharp
new VarnFunctionSignature(
    "net.get",
    [VarnType.String],
    VarnType.String,
    Effect: "network",
    Capability: "network.http")
```

The caller declares `![network]`, the program declares `cap[network.http]`, and the host grants `network.http`.

Embed a module with `engine.AddModule(new TextModule())`, or load a compiled assembly with `varn run program.varn --module ./Example.Text.dll`. Overloads resolve by exact argument types; duplicate name-and-parameter signatures are rejected.

## Optional values

Module signatures use `VarnType.Optional(elementType)`. Handlers return `VarnValue.Some(value)` or `VarnValue.None(elementType)`:

```csharp
builder.Function(
    new VarnFunctionSignature(
        "cache.lookup",
        [VarnType.String],
        VarnType.Optional(VarnType.String)),
    static (_, arguments, _) => ValueTask.FromResult(
        VarnValue.None(VarnType.String)));
```

`VarnValue.IsSome` tests presence and `AsOptionalValue()` extracts a present value in trusted host code. The SDK factories reject `null`, `any`, and nested optional element types. Varn programs do not receive an unchecked extraction function; they use `if let`.

## Security boundary

An `IVarnModule` assembly executes as trusted .NET host code. Varn gates calls into it, but cannot stop malicious initialization or handler code from using ambient .NET APIs. Do not load untrusted assemblies.

Network and filesystem modules should receive narrowly configured host services, enforce policy themselves, charge work with `context.ConsumeStep()`, honor cancellation, and expose deterministic error values rather than host exceptions. Process isolation, signed manifests, and a restricted module host are future security work.
