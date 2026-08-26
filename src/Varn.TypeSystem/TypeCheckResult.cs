using Varn.Syntax;

namespace Varn.TypeSystem;

public sealed record TypeCheckResult(IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}
