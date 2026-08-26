namespace Varn.Syntax;

public sealed record ProgramSyntax(
    IReadOnlyList<string> Capabilities,
    long? StepBudget,
    IReadOnlyList<FunctionSyntax> Functions,
    SourceSpan Span);

public sealed record FunctionSyntax(
    string Name,
    IReadOnlyList<ParameterSyntax> Parameters,
    VarnType ReturnType,
    IReadOnlyList<string> Effects,
    IReadOnlyList<StatementSyntax> Body,
    SourceSpan Span);

public sealed record ParameterSyntax(string Name, VarnType Type, SourceSpan Span);

public abstract record StatementSyntax(SourceSpan Span);

public sealed record LetStatementSyntax(
    string Name,
    VarnType Type,
    ExpressionSyntax Value,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public sealed record ExpressionStatementSyntax(
    ExpressionSyntax Expression,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public sealed record ReturnStatementSyntax(
    ExpressionSyntax Value,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public sealed record IfStatementSyntax(
    ExpressionSyntax Condition,
    IReadOnlyList<StatementSyntax> ThenBody,
    IReadOnlyList<StatementSyntax> ElseBody,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public sealed record LoopStatementSyntax(
    string Iterator,
    VarnType IteratorType,
    long StartInclusive,
    long EndExclusive,
    long MaxIterations,
    IReadOnlyList<StatementSyntax> Body,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public abstract record ExpressionSyntax(SourceSpan Span);

public sealed record LiteralExpressionSyntax(
    object? Value,
    VarnType Type,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record ReferenceExpressionSyntax(
    string Name,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record CallExpressionSyntax(
    string FunctionName,
    IReadOnlyList<ExpressionSyntax> Arguments,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);
