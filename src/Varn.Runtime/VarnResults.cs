using Varn.ModuleSdk;
using Varn.Syntax;

namespace Varn.Runtime;

public sealed record VarnCheckResult(ProgramSyntax Program, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

public sealed record VarnRunResult(VarnValue? ReturnValue, IReadOnlyList<Diagnostic> Diagnostics, long Steps)
{
    public bool IsSuccess => Diagnostics.Count == 0;

    public int ExitCode => IsSuccess && ReturnValue is { } value && value.Type == VarnType.I64
        ? checked((int)value.AsI64())
        : 1;
}

public sealed class VarnRunOptions
{
    public ISet<string> AllowedCapabilities { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public long MaxSteps { get; init; } = 100_000;

    public TextWriter Output { get; init; } = Console.Out;
}
