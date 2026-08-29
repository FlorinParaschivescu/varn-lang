namespace Varn.Syntax;

public sealed record ProgramSyntax(
    IReadOnlyList<string> Capabilities,
    long? StepBudget,
    IReadOnlyList<RecordSyntax> Records,
    IReadOnlyList<FunctionSyntax> Functions,
    SourceSpan Span);

public sealed record RecordSyntax(
    string Name,
    IReadOnlyList<RecordFieldSyntax> Fields,
    SourceSpan Span);

public sealed record RecordFieldSyntax(string Name, VarnType Type, SourceSpan Span);

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

public sealed record VarStatementSyntax(
    string Name,
    VarnType Type,
    ExpressionSyntax Value,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public sealed record SetStatementSyntax(
    string Name,
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

public sealed record IfLetStatementSyntax(
    string Binding,
    VarnType BindingType,
    ExpressionSyntax Optional,
    IReadOnlyList<StatementSyntax> ThenBody,
    IReadOnlyList<StatementSyntax> ElseBody,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public sealed record IfOkStatementSyntax(
    string Binding,
    VarnType BindingType,
    ExpressionSyntax Result,
    IReadOnlyList<StatementSyntax> ThenBody,
    string? ErrorBinding,
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

public sealed record EachStatementSyntax(
    string Iterator,
    VarnType IteratorType,
    ExpressionSyntax List,
    long MaxIterations,
    IReadOnlyList<StatementSyntax> Body,
    SourceSpan SourceSpan) : StatementSyntax(SourceSpan);

public abstract record ExpressionSyntax(SourceSpan Span);

public sealed record LiteralExpressionSyntax(
    object? Value,
    VarnType Type,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record SomeExpressionSyntax(
    ExpressionSyntax Value,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

/// <summary>
/// A typed absence. The element type is written only where the context cannot supply it; the
/// checker fills <see cref="InferredElementType"/> in before the interpreter or the canonical
/// formatter reads <see cref="ElementType"/>, so both spellings behave identically.
/// </summary>
public sealed record NoneExpressionSyntax(
    VarnType? DeclaredElementType,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan)
{
    public VarnType? InferredElementType { get; set; }

    public VarnType ElementType => DeclaredElementType ?? InferredElementType ?? VarnType.Null;
}

public sealed record OkExpressionSyntax(
    ExpressionSyntax Value,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record ErrExpressionSyntax(
    VarnType? DeclaredValueType,
    ExpressionSyntax Error,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan)
{
    public VarnType? InferredValueType { get; set; }

    public VarnType ValueType => DeclaredValueType ?? InferredValueType ?? VarnType.Null;
}

public sealed record ListExpressionSyntax(
    VarnType? DeclaredElementType,
    IReadOnlyList<ExpressionSyntax> Elements,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan)
{
    public VarnType? InferredElementType { get; set; }

    public VarnType ElementType => DeclaredElementType ?? InferredElementType ?? VarnType.Null;
}

public sealed record RecordExpressionSyntax(
    string TypeName,
    IReadOnlyList<RecordInitializerSyntax> Fields,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record RecordInitializerSyntax(
    string Name,
    ExpressionSyntax Value,
    SourceSpan Span);

public sealed record FieldExpressionSyntax(
    ExpressionSyntax Target,
    string FieldName,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record ReferenceExpressionSyntax(
    string Name,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

/// <summary>
/// A short-circuiting boolean operator. It cannot desugar to a call the way arithmetic does,
/// because not evaluating the right operand is the whole point.
/// </summary>
public sealed record LogicalExpressionSyntax(
    ExpressionSyntax Left,
    bool IsAnd,
    ExpressionSyntax Right,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan);

public sealed record CallExpressionSyntax(
    string FunctionName,
    IReadOnlyList<ExpressionSyntax> Arguments,
    SourceSpan SourceSpan) : ExpressionSyntax(SourceSpan)
{
    /// <summary>
    /// The operator this call was desugared from, where it was. A failure must name what the
    /// source says: 'div' is no longer spellable, so reporting it would name nothing the reader
    /// can find.
    /// </summary>
    public string? OperatorSpelling { get; init; }

    public string SourceName => OperatorSpelling ?? FunctionName;
}
