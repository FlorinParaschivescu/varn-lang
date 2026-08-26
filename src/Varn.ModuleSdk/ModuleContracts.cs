using Varn.Syntax;

namespace Varn.ModuleSdk;

public interface IVarnModule
{
    string Name { get; }

    void Register(VarnModuleBuilder builder);
}

public sealed record VarnFunctionSignature(
    string Name,
    IReadOnlyList<VarnType> Parameters,
    VarnType ReturnType,
    string? Effect = null,
    string? Capability = null);

public delegate ValueTask<VarnValue> VarnFunctionHandler(
    VarnCallContext context,
    IReadOnlyList<VarnValue> arguments,
    CancellationToken cancellationToken);

public sealed record RegisteredVarnFunction(
    string ModuleName,
    VarnFunctionSignature Signature,
    VarnFunctionHandler Handler);

public sealed class VarnCallContext
{
    private readonly Action _consumeStep;

    internal VarnCallContext(TextWriter output, Action consumeStep)
    {
        Output = output;
        _consumeStep = consumeStep;
    }

    public TextWriter Output { get; }

    public void ConsumeStep() => _consumeStep();
}
