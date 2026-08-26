# Example external module

`Varn.ExampleModule` is a minimal out-of-tree-style .NET 10 module. It references only `Varn.ModuleSdk` and registers the pure function `text.length(str)->i64`.

From the repository root:

```sh
dotnet build examples/modules/Varn.ExampleModule
dotnet run --project src/Varn.Cli -- run examples/module-demo.varn \
  --module examples/modules/Varn.ExampleModule/bin/Debug/net10.0/Varn.ExampleModule.dll \
  --allow console.write
```

Expected output: `14`.

The CLI loads every public `IVarnModule` implementation with a public parameterless constructor from the supplied assembly. Module assemblies are trusted host code; do not load untrusted DLLs.
