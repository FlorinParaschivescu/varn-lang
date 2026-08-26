using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.ExampleModule;

public sealed class TextModule : IVarnModule
{
    public string Name => "example.text";

    public void Register(VarnModuleBuilder builder)
    {
        builder.Function(
            new VarnFunctionSignature("text.length", [VarnType.String], VarnType.I64),
            static (_, arguments, _) => ValueTask.FromResult(
                VarnValue.From((long)((string)arguments[0].Value!).Length)));
    }
}
