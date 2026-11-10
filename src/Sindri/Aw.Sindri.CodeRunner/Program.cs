using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.CodeRunner;

/// <summary>
/// 'sindri-code-runner &lt;assembly&gt; [--memory-mb N]': loads an assembly the control plane compiled, reads the input JSON on stdin, calls 'SindriCodeBody.Run(JsonNode? input)'
/// and writes the result as JSON on stdout. The timeout is the caller's (it kills this process); the memory limit is the managed heap's. Exit codes: 0 done, 2 usage or the
/// assembly can't be loaded, 3 out of memory, 4 the code threw or its result is not JSON (the first line of stderr says why).
/// </summary>
internal static class Program
{
    private const int Usage = 2;
    private const int OutOfMemory = 3;
    private const int Failed = 4;

    private static int Main(string[] args)
    {
        if (args.Length is not (1 or 3) || (args.Length == 3 && (args[1] != "--memory-mb" || !int.TryParse(args[2], out var mb) || mb < 1)))
        {
            return Fail(Usage, "usage: sindri-code-runner <assembly> [--memory-mb N]");
        }

        if (args.Length == 3)
        {
            // why: a managed-heap cap for this process; the sandbox does not bound a plain process's memory
            AppContext.SetData("GCHeapHardLimit", (ulong)int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) * 1024 * 1024);
            GC.RefreshMemoryLimit();
        }

        MethodInfo? run;
        try
        {
            run = Assembly.LoadFrom(args[0]).GetType("SindriCodeBody")?.GetMethod("Run");
        }
        catch (Exception e) when (e is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            return Fail(Usage, $"can't load '{args[0]}': {e.Message}");
        }

        return run is null ? Fail(Usage, $"'{args[0]}' has no SindriCodeBody.Run") : Execute(run);
    }

    private static int Execute(MethodInfo run)
    {
        try
        {
            var input = JsonNode.Parse(Console.In.ReadToEnd());
            var result = run.Invoke(null, [input]);
            Console.Out.Write(result is null ? "null" : JsonSerializer.Serialize(result, result.GetType()));
            return 0;
        }
        catch (TargetInvocationException e) when (e.InnerException is OutOfMemoryException)
        {
            return Fail(OutOfMemory, "out of memory");
        }
        catch (OutOfMemoryException)
        {
            return Fail(OutOfMemory, "out of memory");
        }
        catch (TargetInvocationException e)
        {
            return Fail(Failed, $"{e.InnerException?.GetType().Name}: {e.InnerException?.Message}");
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            return Fail(Failed, $"{e.GetType().Name}: {e.Message}");
        }
    }

    private static int Fail(int code, string message)
    {
        Console.Error.WriteLine(message);
        return code;
    }
}
