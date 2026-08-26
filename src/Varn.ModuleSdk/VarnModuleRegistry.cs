using Varn.Syntax;

namespace Varn.ModuleSdk;

public sealed class VarnModuleRegistry
{
    private readonly Dictionary<string, List<RegisteredVarnFunction>> _functions =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<RegisteredVarnFunction> Functions =>
        _functions.Values.SelectMany(static overloads => overloads).ToArray();

    public void Add(IVarnModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        var builder = new VarnModuleBuilder(module.Name, this);
        module.Register(builder);
    }

    public IReadOnlyList<RegisteredVarnFunction> Find(string name) =>
        _functions.TryGetValue(name, out var overloads) ? overloads : [];

    public RegisteredVarnFunction? Resolve(string name, IReadOnlyList<VarnType> argumentTypes)
    {
        return Find(name).SingleOrDefault(function =>
            function.Signature.Parameters.Count == argumentTypes.Count &&
            function.Signature.Parameters.Zip(argumentTypes).All(static pair =>
                pair.First == VarnType.Any || pair.First == pair.Second));
    }

    internal void Register(
        string moduleName,
        VarnFunctionSignature signature,
        VarnFunctionHandler handler)
    {
        ValidateSignature(signature);
        var overloads = _functions.GetValueOrDefault(signature.Name);
        if (overloads is null)
        {
            overloads = [];
            _functions.Add(signature.Name, overloads);
        }

        if (overloads.Any(existing => existing.Signature.Parameters.SequenceEqual(signature.Parameters)))
        {
            throw new InvalidOperationException(
                $"Function '{signature.Name}' with the same parameter types is already registered.");
        }

        overloads.Add(new RegisteredVarnFunction(moduleName, signature, handler));
    }

    private static void ValidateSignature(VarnFunctionSignature signature)
    {
        if (string.IsNullOrWhiteSpace(signature.Name))
        {
            throw new ArgumentException("A module function must have a name.", nameof(signature));
        }

        if ((signature.Effect is null) != (signature.Capability is null))
        {
            throw new ArgumentException(
                "Effectful module functions must declare both an effect and a capability.",
                nameof(signature));
        }
    }
}

public sealed class VarnModuleBuilder
{
    private readonly string _moduleName;
    private readonly VarnModuleRegistry _registry;

    internal VarnModuleBuilder(string moduleName, VarnModuleRegistry registry)
    {
        _moduleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("A module must have a name.", nameof(moduleName))
            : moduleName;
        _registry = registry;
    }

    public VarnModuleBuilder Function(
        VarnFunctionSignature signature,
        VarnFunctionHandler handler)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(handler);
        _registry.Register(_moduleName, signature, handler);
        return this;
    }
}
