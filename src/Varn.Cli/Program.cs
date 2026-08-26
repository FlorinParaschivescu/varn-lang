using System.Reflection;
using System.Runtime.Loader;
using Varn.ModuleSdk;
using Varn.Modules.Standard;
using Varn.Runtime;
using Varn.Syntax;

namespace Varn.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("run" or "check" or "inspect"))
        {
            PrintUsage();
            return 2;
        }

        try
        {
            var command = args[0];
            var file = Path.GetFullPath(args[1]);
            var parsedOptions = ParseOptions(args[2..]);
            var engine = new VarnEngine([new CoreModule(), new ConsoleModule()]);
            foreach (var modulePath in parsedOptions.ModulePaths)
            {
                foreach (var module in LoadModules(modulePath))
                {
                    engine.AddModule(module);
                }
            }

            var source = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            return command switch
            {
                "check" => Check(engine, source),
                "inspect" => Inspect(engine, source),
                "run" => await RunAsync(engine, source, parsedOptions).ConfigureAwait(false),
                _ => 2
            };
        }
        catch (CliException exception)
        {
            Console.Error.WriteLine($"varn: {exception.Message}");
            return 2;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"varn: {exception.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"varn: {exception.Message}");
            return 2;
        }
    }

    private static int Check(VarnEngine engine, string source)
    {
        var result = engine.Check(source);
        if (!result.IsValid)
        {
            PrintDiagnostics(result.Diagnostics);
            return 1;
        }

        Console.WriteLine("valid");
        return 0;
    }

    private static int Inspect(VarnEngine engine, string source)
    {
        var result = engine.Check(source);
        if (!result.IsValid)
        {
            PrintDiagnostics(result.Diagnostics);
            return 1;
        }

        Console.WriteLine(CanonicalFormatter.Format(result.Program));
        return 0;
    }

    private static async Task<int> RunAsync(VarnEngine engine, string source, CliOptions parsedOptions)
    {
        var result = await engine.RunAsync(
            source,
            new VarnRunOptions
            {
                AllowedCapabilities = parsedOptions.AllowedCapabilities,
                MaxSteps = parsedOptions.MaxSteps,
                Output = Console.Out
            }).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            PrintDiagnostics(result.Diagnostics);
            return 1;
        }

        return result.ExitCode;
    }

    private static CliOptions ParseOptions(IReadOnlyList<string> args)
    {
        var allowedCapabilities = new HashSet<string>(StringComparer.Ordinal);
        var modulePaths = new List<string>();
        var maxSteps = 100_000L;
        for (var index = 0; index < args.Count; index++)
        {
            var option = args[index];
            if (index + 1 >= args.Count)
            {
                throw new CliException($"Option '{option}' requires a value.");
            }

            var value = args[++index];
            switch (option)
            {
                case "--allow":
                    allowedCapabilities.Add(value);
                    break;
                case "--module":
                    modulePaths.Add(Path.GetFullPath(value));
                    break;
                case "--max-steps" when long.TryParse(value, out var parsed) && parsed > 0:
                    maxSteps = parsed;
                    break;
                case "--max-steps":
                    throw new CliException("--max-steps must be a positive integer.");
                default:
                    throw new CliException($"Unknown option '{option}'.");
            }
        }

        return new CliOptions(allowedCapabilities, modulePaths, maxSteps);
    }

    private static IReadOnlyList<IVarnModule> LoadModules(string path)
    {
        if (!File.Exists(path))
        {
            throw new CliException($"Module assembly '{path}' does not exist.");
        }

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        var modules = assembly.GetExportedTypes()
            .Where(static type => typeof(IVarnModule).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(static type => (IVarnModule)Activator.CreateInstance(type)!)
            .ToArray();
        if (modules.Length == 0)
        {
            throw new CliException($"Assembly '{path}' exports no IVarnModule implementation with a public parameterless constructor.");
        }

        return modules;
    }

    private static void PrintDiagnostics(IEnumerable<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Console.Error.WriteLine(diagnostic);
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Varn v0.1 bootstrap");
        Console.Error.WriteLine("usage: varn check <program.varn> [--module <assembly>]");
        Console.Error.WriteLine("       varn inspect <program.varn> [--module <assembly>]");
        Console.Error.WriteLine("       varn run <program.varn> [--allow <capability>] [--max-steps <count>] [--module <assembly>]");
    }

    private sealed record CliOptions(
        ISet<string> AllowedCapabilities,
        IReadOnlyList<string> ModulePaths,
        long MaxSteps);

    private sealed class CliException(string message) : Exception(message);
}
