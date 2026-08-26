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

    /// <summary>
    /// A successful i64 result is its own process exit code. A successful structured result exits
    /// 0 and is carried in <see cref="ReturnValue"/> instead; a failed run always exits 1.
    /// </summary>
    public int ExitCode
    {
        get
        {
            if (!IsSuccess || ReturnValue is not { } value)
            {
                return 1;
            }

            return value.Type == VarnType.I64 ? checked((int)value.AsI64()) : 0;
        }
    }
}

public sealed class VarnRunOptions
{
    public ISet<string> AllowedCapabilities { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public long MaxSteps { get; init; } = 100_000;

    public TextWriter Output { get; init; } = Console.Out;

    /// <summary>
    /// Host-supplied JSON bound to the record the program declares as its input, or <c>null</c>
    /// when the program declares none. The host supplies data here rather than in the source, so
    /// one checked program stays reusable across many inputs.
    /// </summary>
    public string? Input { get; init; }
}
