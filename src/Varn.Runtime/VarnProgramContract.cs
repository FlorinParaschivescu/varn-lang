using Varn.Syntax;

namespace Varn.Runtime;

/// <summary>
/// Derives a checked program's structural contract: its record shapes, the record it accepts as
/// host input, and the type it returns. The host reads this contract to learn what a reusable
/// program expects without inspecting its source.
/// </summary>
public static class VarnProgramContract
{
    public const string EntryPointName = "main";

    public static IReadOnlyDictionary<string, VarnRecordShape> RecordShapes(ProgramSyntax program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var shapes = new Dictionary<string, VarnRecordShape>(StringComparer.Ordinal);
        foreach (var record in program.Records)
        {
            shapes[record.Name] = new VarnRecordShape(
                record.Name,
                [.. record.Fields.Select(static field => new VarnRecordField(field.Name, field.Type))]);
        }

        return shapes;
    }

    public static VarnRecordShape? InputShape(ProgramSyntax program)
    {
        ArgumentNullException.ThrowIfNull(program);
        var entryPoint = EntryPoint(program);
        if (entryPoint is null || entryPoint.Parameters.Count != 1)
        {
            return null;
        }

        return RecordShapes(program).GetValueOrDefault(entryPoint.Parameters[0].Type.Name);
    }

    public static VarnType? ResultType(ProgramSyntax program) => EntryPoint(program)?.ReturnType;

    private static FunctionSyntax? EntryPoint(ProgramSyntax program) =>
        program.Functions.FirstOrDefault(static function =>
            string.Equals(function.Name, EntryPointName, StringComparison.Ordinal));
}
