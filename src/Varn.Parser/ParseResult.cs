using Varn.Syntax;

namespace Varn.Parser;

public sealed record ParseResult(ProgramSyntax Program, IReadOnlyList<Diagnostic> Diagnostics);
