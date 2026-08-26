using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.Modules.Standard;

public sealed class ConsoleModule : IVarnModule
{
    public const string WriteCapability = "console.write";
    public const string ConsoleEffect = "console";

    public string Name => "varn.console";

    public void Register(VarnModuleBuilder builder)
    {
        builder.Function(
            new VarnFunctionSignature(
                "io.print",
                [VarnType.Any],
                VarnType.Null,
                ConsoleEffect,
                WriteCapability),
            static async (context, arguments, cancellationToken) =>
            {
                context.ConsumeStep();
                await context.Output.WriteLineAsync(arguments[0].ToCanonicalString().AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                return VarnValue.Null;
            });
    }
}
