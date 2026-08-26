using System.Text.Json;

namespace Varn.Bench;

/// <summary>
/// How a candidate solution behaved across every case of its task. The ordering matters: a defect
/// caught before execution is strictly better than one that aborts, which is strictly better than
/// one that returns a plausible wrong answer nobody notices.
/// </summary>
public enum Outcome
{
    Correct,
    Rejected,
    Crashed,
    SilentWrong
}

public sealed record CaseOutcome(string Name, bool Matched, string Expected, string Actual, string? Failure);

public sealed record SolutionOutcome(
    string Task,
    string Variant,
    string Language,
    string AuthorIntent,
    Outcome Outcome,
    string? RejectionReason,
    int Characters,
    int ApproximateTokens,
    IReadOnlyList<CaseOutcome> Cases)
{
    public bool MatchedIntent => Outcome switch
    {
        Outcome.Correct => AuthorIntent == "correct",
        Outcome.SilentWrong => AuthorIntent == "silent_wrong",
        Outcome.Crashed => AuthorIntent is "crashed" or "silent_wrong",
        Outcome.Rejected => AuthorIntent is not "correct",
        _ => false
    };
}

public static class Grading
{
    /// <summary>
    /// Compares two JSON documents exactly, including number formatting. A discount of 235.0 where
    /// the contract says a whole number is counted wrong, because the host receives a value its
    /// declared type does not admit. Every mismatch is reported verbatim so the reader can judge.
    /// </summary>
    public static bool Matches(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedProperties = expected.EnumerateObject().ToArray();
                var actualProperties = actual.EnumerateObject().ToArray();
                if (expectedProperties.Length != actualProperties.Length)
                {
                    return false;
                }

                foreach (var property in expectedProperties)
                {
                    if (!actual.TryGetProperty(property.Name, out var actualValue) ||
                        !Matches(property.Value, actualValue))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToArray();
                var actualItems = actual.EnumerateArray().ToArray();
                return expectedItems.Length == actualItems.Length &&
                    expectedItems.Zip(actualItems).All(pair => Matches(pair.First, pair.Second));
            case JsonValueKind.Number:
                return string.Equals(expected.GetRawText(), actual.GetRawText(), StringComparison.Ordinal);
            case JsonValueKind.String:
                return string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal);
            default:
                return true;
        }
    }

    /// <summary>
    /// A deterministic, dependency-free proxy for tokenization: identifier runs, number runs, and
    /// individual symbols. It is not any model's tokenizer, and the report says so. It exists to
    /// compare two sources on the same ruler, not to predict a billing line.
    /// </summary>
    public static int ApproximateTokens(string source)
    {
        var tokens = 0;
        var index = 0;
        while (index < source.Length)
        {
            var current = source[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_'))
                {
                    index++;
                }
            }
            else if (char.IsDigit(current))
            {
                while (index < source.Length && (char.IsDigit(source[index]) || source[index] == '.'))
                {
                    index++;
                }
            }
            else
            {
                index++;
            }

            tokens++;
        }

        return tokens;
    }
}
