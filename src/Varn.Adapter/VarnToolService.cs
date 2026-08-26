using System.Text;
using Varn.Modules.Standard;
using Varn.Runtime;
using Varn.Syntax;

namespace Varn.Adapter;

public sealed class VarnToolService
{
    public const int MaximumSourceCharacters = 1_000_000;
    public const long MaximumRunSteps = 1_000_000;
    public const int MaximumOutputCharacters = 1_000_000;

    private static readonly VarnSpanResponse HostPolicySpan = new(0, 0);

    private readonly VarnEngine _engine = new([new CoreModule(), new ConsoleModule()]);
    private readonly HashSet<string> _supportedCapabilities;

    public VarnToolService()
    {
        _supportedCapabilities = _engine.Modules.Functions
            .Select(static function => function.Signature.Capability)
            .Where(static capability => capability is not null)
            .Select(static capability => capability!)
            .ToHashSet(StringComparer.Ordinal);
    }

    public VarnCheckResponse Check(string? source)
    {
        if (ValidateSource(source) is { } failure)
        {
            return new VarnCheckResponse(VarnJsonFormatter.SchemaVersion, "check", false, [failure]);
        }

        return VarnJsonFormatter.CreateCheckResponse(_engine.Check(source!));
    }

    public VarnInspectionResponse Inspect(string? source)
    {
        if (ValidateSource(source) is { } failure)
        {
            return new VarnInspectionResponse(VarnJsonFormatter.SchemaVersion, "inspect", false, null, [failure]);
        }

        var check = _engine.Check(source!);
        var canonical = check.IsValid ? CanonicalFormatter.Format(check.Program) : null;
        return VarnJsonFormatter.CreateInspectionResponse(check, canonical);
    }

    public async ValueTask<VarnRunResponse> RunAsync(
        string? source,
        IReadOnlyList<string>? allowedCapabilities,
        long maxSteps,
        int maxOutputCharacters,
        string? input = null,
        CancellationToken cancellationToken = default)
    {
        if (ValidateSource(source) is { } sourceFailure)
        {
            return RunFailure(sourceFailure);
        }

        if (ValidateCapabilities(allowedCapabilities) is { } capabilityFailure)
        {
            return RunFailure(capabilityFailure);
        }

        if (maxSteps is < 1 or > MaximumRunSteps)
        {
            return RunFailure(PolicyDiagnostic(
                "VARN5003",
                $"maxSteps must be between 1 and {MaximumRunSteps}."));
        }

        if (maxOutputCharacters is < 1 or > MaximumOutputCharacters)
        {
            return RunFailure(PolicyDiagnostic(
                "VARN5004",
                $"maxOutputCharacters must be between 1 and {MaximumOutputCharacters}."));
        }

        var output = new BoundedTextWriter(maxOutputCharacters);
        var result = await _engine.RunAsync(
            source!,
            new VarnRunOptions
            {
                AllowedCapabilities = allowedCapabilities!.ToHashSet(StringComparer.Ordinal),
                MaxSteps = maxSteps,
                Output = output,
                Input = input
            },
            cancellationToken).ConfigureAwait(false);

        if (output.LimitExceeded)
        {
            var diagnostics = result.Diagnostics
                .Append(new Diagnostic(
                    "VARN5005",
                    $"Execution output exceeded the host ceiling of {maxOutputCharacters} characters and was truncated.",
                    new SourceSpan(0, 0)))
                .ToArray();
            result = new VarnRunResult(result.ReturnValue, diagnostics, result.Steps);
        }

        return VarnJsonFormatter.CreateRunResponse(result, output.ToString());
    }

    private static VarnDiagnosticResponse? ValidateSource(string? source)
    {
        if (source is null)
        {
            return PolicyDiagnostic("VARN5001", "source is required.");
        }

        return source.Length > MaximumSourceCharacters
            ? PolicyDiagnostic(
                "VARN5001",
                $"source exceeds the host ceiling of {MaximumSourceCharacters} characters.")
            : null;
    }

    private VarnDiagnosticResponse? ValidateCapabilities(IReadOnlyList<string>? allowedCapabilities)
    {
        if (allowedCapabilities is null)
        {
            return PolicyDiagnostic(
                "VARN5002",
                "allowedCapabilities is required; pass an empty array to grant no capabilities.");
        }

        if (allowedCapabilities.Any(string.IsNullOrWhiteSpace))
        {
            return PolicyDiagnostic("VARN5002", "Capability grants must be non-empty exact identifiers.");
        }

        var unsupported = allowedCapabilities
            .Where(capability => !_supportedCapabilities.Contains(capability))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return unsupported.Length > 0
            ? PolicyDiagnostic(
                "VARN5002",
                $"The local adapter does not expose these capabilities: {string.Join(", ", unsupported)}.")
            : null;
    }

    private static VarnRunResponse RunFailure(VarnDiagnosticResponse diagnostic) =>
        new(
            VarnJsonFormatter.SchemaVersion,
            "run",
            false,
            1,
            0,
            null,
            string.Empty,
            [diagnostic]);

    private static VarnDiagnosticResponse PolicyDiagnostic(string code, string message) =>
        new(code, message, HostPolicySpan);

    private sealed class BoundedTextWriter(int maximumCharacters) : TextWriter
    {
        private readonly StringBuilder _buffer = new(Math.Min(maximumCharacters, 4_096));

        public override Encoding Encoding => Encoding.UTF8;

        public bool LimitExceeded { get; private set; }

        public override void Write(char value) => Append([value]);

        public override void Write(string? value)
        {
            if (value is not null)
            {
                Append(value.AsSpan());
            }
        }

        public override void Write(char[] buffer, int index, int count) =>
            Append(buffer.AsSpan(index, count));

        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Append(buffer.Span);
            return Task.CompletedTask;
        }

        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Append(buffer.Span);
            Append(NewLine.AsSpan());
            return Task.CompletedTask;
        }

        public override string ToString() => _buffer.ToString();

        private void Append(ReadOnlySpan<char> value)
        {
            var available = maximumCharacters - _buffer.Length;
            if (value.Length > available)
            {
                LimitExceeded = true;
            }

            if (available > 0)
            {
                _buffer.Append(value[..Math.Min(value.Length, available)]);
            }
        }
    }
}
