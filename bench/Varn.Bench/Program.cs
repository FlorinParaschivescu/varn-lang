using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Varn.Bench;
using Varn.ModuleSdk;
using Varn.Modules.Standard;
using Varn.Runtime;

var root = FindRepositoryRoot();
var pythonCommand = ArgumentValue(args, "--python") ?? DefaultPython();
var outputDirectory = ArgumentValue(args, "--out") ?? Path.Combine(root, "bench", "results");

var tasks = Directory.GetFiles(Path.Combine(root, "bench", "tasks"), "*.json")
    .OrderBy(static path => path, StringComparer.Ordinal)
    .Select(path => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone())
    .ToArray();

var manifest = JsonDocument
    .Parse(File.ReadAllText(Path.Combine(root, "bench", "solutions", "manifest.json")))
    .RootElement.EnumerateArray()
    .Select(static entry => (
        Task: entry.GetProperty("task").GetString()!,
        Variant: entry.GetProperty("variant").GetString()!,
        Language: entry.GetProperty("language").GetString()!,
        Note: entry.GetProperty("note").GetString()!,
        AuthorIntent: entry.GetProperty("authorIntent").GetString()!))
    .ToArray();

var pythonAvailable = await IsPythonAvailableAsync(pythonCommand).ConfigureAwait(false);
if (!pythonAvailable)
{
    Console.Error.WriteLine(
        $"warning: '{pythonCommand}' is unavailable, so the Python arm is skipped. Pass --python <path> to enable it.");
}

var outcomes = new List<SolutionOutcome>();
foreach (var entry in manifest)
{
    if (entry.Language == "python" && !pythonAvailable)
    {
        continue;
    }

    var task = tasks.Single(candidate => candidate.GetProperty("id").GetString() == entry.Task);
    var extension = entry.Language == "varn" ? "varn" : "py";
    var path = Path.Combine(root, "bench", "solutions", entry.Language, entry.Task, $"{entry.Variant}.{extension}");
    var source = await File.ReadAllTextAsync(path).ConfigureAwait(false);

    var outcome = entry.Language == "varn"
        ? await GradeVarnAsync(task, entry.Variant, entry.AuthorIntent, source).ConfigureAwait(false)
        : await GradePythonAsync(task, entry.Variant, entry.AuthorIntent, path, pythonCommand).ConfigureAwait(false);
    outcomes.Add(outcome);
}

Directory.CreateDirectory(outputDirectory);
var report = BuildReport(outcomes, pythonAvailable);
await File.WriteAllTextAsync(Path.Combine(outputDirectory, "results.md"), report).ConfigureAwait(false);
await File.WriteAllTextAsync(
    Path.Combine(outputDirectory, "results.json"),
    JsonSerializer.Serialize(outcomes, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);

Console.WriteLine(report);

var mismatched = outcomes.Where(static outcome => !outcome.MatchedIntent).ToArray();
foreach (var outcome in mismatched)
{
    Console.Error.WriteLine(
        $"harness check: {outcome.Language}/{outcome.Task}/{outcome.Variant} was written to be " +
        $"'{outcome.AuthorIntent}' but graded '{Describe(outcome.Outcome)}'.");
}

return mismatched.Length == 0 ? 0 : 1;

async Task<SolutionOutcome> GradeVarnAsync(JsonElement task, string variant, string intent, string source)
{
    var taskId = task.GetProperty("id").GetString()!;
    var engine = new VarnEngine([new CoreModule(), new ConsoleModule()]);
    var check = engine.Check(source);
    if (!check.IsValid)
    {
        return new SolutionOutcome(
            taskId,
            variant,
            "varn",
            intent,
            Outcome.Rejected,
            string.Join("; ", check.Diagnostics.Select(static diagnostic => $"{diagnostic.Code} {diagnostic.Message}")),
            source.Length,
            Grading.ApproximateTokens(source),
            []);
    }

    var cases = new List<CaseOutcome>();
    foreach (var scenario in task.GetProperty("cases").EnumerateArray())
    {
        var expected = scenario.GetProperty("expect");
        var result = await engine.RunAsync(
            source,
            new VarnRunOptions
            {
                Input = scenario.GetProperty("input").GetRawText(),
                MaxSteps = 10_000,
                Output = TextWriter.Null
            }).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            cases.Add(new CaseOutcome(
                scenario.GetProperty("name").GetString()!,
                false,
                expected.GetRawText(),
                string.Empty,
                string.Join("; ", result.Diagnostics.Select(static diagnostic => $"{diagnostic.Code} {diagnostic.Message}"))));
            continue;
        }

        var actual = Normalize(result.ReturnValue!.Value);
        using var actualDocument = JsonDocument.Parse(actual);
        cases.Add(new CaseOutcome(
            scenario.GetProperty("name").GetString()!,
            Grading.Matches(expected, actualDocument.RootElement),
            expected.GetRawText(),
            actual,
            null));
    }

    return new SolutionOutcome(
        taskId,
        variant,
        "varn",
        intent,
        Classify(cases),
        null,
        source.Length,
        Grading.ApproximateTokens(source),
        cases);
}

async Task<SolutionOutcome> GradePythonAsync(
    JsonElement task,
    string variant,
    string intent,
    string path,
    string python)
{
    var taskId = task.GetProperty("id").GetString()!;
    var source = await File.ReadAllTextAsync(path).ConfigureAwait(false);
    // compile() checks syntax without writing bytecode, so the harness never litters __pycache__
    // directories into the solution tree.
    var compile = await RunProcessAsync(
        python,
        ["-c", $"compile(open(r'{path}', encoding='utf-8').read(), r'{path}', 'exec')"],
        null).ConfigureAwait(false);
    if (compile.ExitCode != 0)
    {
        return new SolutionOutcome(
            taskId,
            variant,
            "python",
            intent,
            Outcome.Rejected,
            compile.StandardError.Trim(),
            source.Length,
            Grading.ApproximateTokens(source),
            []);
    }

    var cases = new List<CaseOutcome>();
    foreach (var scenario in task.GetProperty("cases").EnumerateArray())
    {
        var expected = scenario.GetProperty("expect");
        var execution = await RunProcessAsync(python, [path], scenario.GetProperty("input").GetRawText())
            .ConfigureAwait(false);
        if (execution.ExitCode != 0)
        {
            cases.Add(new CaseOutcome(
                scenario.GetProperty("name").GetString()!,
                false,
                expected.GetRawText(),
                string.Empty,
                LastLine(execution.StandardError)));
            continue;
        }

        try
        {
            using var actualDocument = JsonDocument.Parse(execution.StandardOutput);
            cases.Add(new CaseOutcome(
                scenario.GetProperty("name").GetString()!,
                Grading.Matches(expected, actualDocument.RootElement),
                expected.GetRawText(),
                execution.StandardOutput.Trim(),
                null));
        }
        catch (JsonException exception)
        {
            cases.Add(new CaseOutcome(
                scenario.GetProperty("name").GetString()!,
                false,
                expected.GetRawText(),
                execution.StandardOutput.Trim(),
                exception.Message));
        }
    }

    return new SolutionOutcome(
        taskId,
        variant,
        "python",
        intent,
        Classify(cases),
        null,
        source.Length,
        Grading.ApproximateTokens(source),
        cases);
}

static Outcome Classify(IReadOnlyList<CaseOutcome> cases)
{
    if (cases.Any(static item => item.Failure is not null))
    {
        return Outcome.Crashed;
    }

    return cases.All(static item => item.Matched) ? Outcome.Correct : Outcome.SilentWrong;
}

static string Normalize(VarnValue value)
{
    if (value.IsResult)
    {
        var result = value.AsResult();
        return result.IsOk
            ? $"{{\"ok\":true,\"value\":{Normalize(result.Value)}}}"
            : $"{{\"ok\":false,\"error\":{JsonSerializer.Serialize(result.Value.Value as string)}}}";
    }

    if (value.IsRecord)
    {
        var record = value.AsRecord();
        var fields = record.Shape.Fields
            .Select((field, index) => $"{JsonSerializer.Serialize(field.Name)}:{Normalize(record.Values[index])}");
        return $"{{{string.Join(",", fields)}}}";
    }

    if (value.Type.IsOptional)
    {
        return value.IsSome ? Normalize(value.AsOptionalValue()) : "null";
    }

    if (value.Type.IsList)
    {
        return $"[{string.Join(",", value.AsList().Select(Normalize))}]";
    }

    return value.Type.Name switch
    {
        "i64" => value.AsI64().ToString(CultureInfo.InvariantCulture),
        "f64" => value.AsF64().ToString("R", CultureInfo.InvariantCulture),
        "bool" => value.AsBool() ? "true" : "false",
        "str" => JsonSerializer.Serialize(value.Value as string),
        _ => "null"
    };
}

static string Describe(Outcome outcome) => outcome switch
{
    Outcome.Correct => "correct",
    Outcome.Rejected => "rejected",
    Outcome.Crashed => "crashed",
    _ => "silent_wrong"
};

static string BuildReport(IReadOnlyList<SolutionOutcome> outcomes, bool pythonAvailable)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Varn benchmark results");
    builder.AppendLine();
    builder.AppendLine("Generated by `dotnet run --project bench/Varn.Bench`. Deterministic: no model is called.");
    builder.AppendLine();
    if (!pythonAvailable)
    {
        builder.AppendLine("> The Python arm was skipped because no interpreter was found.");
        builder.AppendLine();
    }

    builder.AppendLine("## Outcome by language");
    builder.AppendLine();
    builder.AppendLine("| Language | Correct | Rejected before running | Crashed | Silent wrong |");
    builder.AppendLine("| --- | --- | --- | --- | --- |");
    foreach (var group in outcomes.GroupBy(static outcome => outcome.Language).OrderBy(static group => group.Key, StringComparer.Ordinal))
    {
        builder.Append("| ").Append(group.Key)
            .Append(" | ").Append(group.Count(static outcome => outcome.Outcome == Outcome.Correct))
            .Append(" | ").Append(group.Count(static outcome => outcome.Outcome == Outcome.Rejected))
            .Append(" | ").Append(group.Count(static outcome => outcome.Outcome == Outcome.Crashed))
            .Append(" | ").Append(group.Count(static outcome => outcome.Outcome == Outcome.SilentWrong))
            .AppendLine(" |");
    }

    builder.AppendLine();
    builder.AppendLine("## Defects, and where each language caught them");
    builder.AppendLine();
    builder.AppendLine("| Task | Defect | Varn | Python |");
    builder.AppendLine("| --- | --- | --- | --- |");
    var defects = outcomes
        .Where(static outcome => outcome.Variant != "reference")
        .GroupBy(static outcome => (outcome.Task, outcome.Variant))
        .OrderBy(static group => group.Key.Task, StringComparer.Ordinal)
        .ThenBy(static group => group.Key.Variant, StringComparer.Ordinal);
    foreach (var defect in defects)
    {
        var varn = defect.FirstOrDefault(static outcome => outcome.Language == "varn");
        var python = defect.FirstOrDefault(static outcome => outcome.Language == "python");
        builder.Append("| ").Append(defect.Key.Task)
            .Append(" | ").Append(defect.Key.Variant)
            .Append(" | ").Append(varn is null ? "not expressible" : Describe(varn.Outcome))
            .Append(" | ").Append(python is null ? "not written" : Describe(python.Outcome))
            .AppendLine(" |");
    }

    builder.AppendLine();
    builder.AppendLine("## Source size of the correct solutions");
    builder.AppendLine();
    builder.AppendLine("| Task | Varn chars | Python chars | Varn tokens | Python tokens |");
    builder.AppendLine("| --- | --- | --- | --- | --- |");
    var references = outcomes.Where(static outcome => outcome.Variant == "reference")
        .GroupBy(static outcome => outcome.Task)
        .OrderBy(static group => group.Key, StringComparer.Ordinal);
    var varnTotal = 0;
    var pythonTotal = 0;
    var varnTokenTotal = 0;
    var pythonTokenTotal = 0;
    foreach (var reference in references)
    {
        var varn = reference.FirstOrDefault(static outcome => outcome.Language == "varn");
        var python = reference.FirstOrDefault(static outcome => outcome.Language == "python");
        varnTotal += varn?.Characters ?? 0;
        pythonTotal += python?.Characters ?? 0;
        varnTokenTotal += varn?.ApproximateTokens ?? 0;
        pythonTokenTotal += python?.ApproximateTokens ?? 0;
        builder.Append("| ").Append(reference.Key)
            .Append(" | ").Append(varn?.Characters.ToString(CultureInfo.InvariantCulture) ?? "-")
            .Append(" | ").Append(python?.Characters.ToString(CultureInfo.InvariantCulture) ?? "-")
            .Append(" | ").Append(varn?.ApproximateTokens.ToString(CultureInfo.InvariantCulture) ?? "-")
            .Append(" | ").Append(python?.ApproximateTokens.ToString(CultureInfo.InvariantCulture) ?? "-")
            .AppendLine(" |");
    }

    builder.Append("| **total** | ").Append(varnTotal)
        .Append(" | ").Append(pythonTotal)
        .Append(" | ").Append(varnTokenTotal)
        .Append(" | ").Append(pythonTokenTotal)
        .AppendLine(" |");
    if (pythonTokenTotal > 0)
    {
        var ratio = (double)varnTokenTotal / pythonTokenTotal;
        builder.AppendLine();
        builder.AppendLine(
            $"Varn uses {ratio.ToString("0.00", CultureInfo.InvariantCulture)} times the approximate tokens of Python for the same correct behaviour.");
    }

    builder.AppendLine();
    builder.AppendLine("Token counts use a deterministic proxy tokenizer, not any model's tokenizer.");
    return builder.ToString();
}

static string LastLine(string text)
{
    var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return lines.Length == 0 ? "process failed" : lines[^1];
}

static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessAsync(
    string fileName,
    IReadOnlyList<string> arguments,
    string? standardInput)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        RedirectStandardInput = standardInput is not null,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
    if (standardInput is not null)
    {
        await process.StandardInput.WriteAsync(standardInput).ConfigureAwait(false);
        process.StandardInput.Close();
    }

    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync().ConfigureAwait(false);
    return (process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
}

static async Task<bool> IsPythonAvailableAsync(string command)
{
    try
    {
        var probe = await RunProcessAsync(command, ["--version"], null).ConfigureAwait(false);
        return probe.ExitCode == 0;
    }
    catch (Exception)
    {
        return false;
    }
}

static string DefaultPython() => OperatingSystem.IsWindows() ? "python" : "python3";

static string? ArgumentValue(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Varn.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName
        ?? throw new DirectoryNotFoundException("Could not find the Varn repository root.");
}
