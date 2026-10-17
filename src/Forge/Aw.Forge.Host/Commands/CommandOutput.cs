using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Forge.Host.Commands;

/// <summary>
/// Maps a command's Result to output and an exit code: 0 success (one JSON document on stdout); 1 invalid input or not
/// found ("error: ..."); 2 a usage error ("usage: ..."); 3 a refusal or an unavailable forge ("refused: ...").
/// Errors are one line on stderr ending with the catalogue code.
/// </summary>
internal static class CommandOutput
{
    internal static int Write<T>(Result<T> result, JsonSerializerOptions json)
    {
        if (result.IsSuccess)
        {
            Console.Out.WriteLine(Encoding.UTF8.GetString(AwJson.Serialize(result.Value, json)));
            return 0;
        }

        var error = result.Error;
        var input = error.Kind is ErrorKind.Invalid or ErrorKind.NotFound;
        Console.Error.WriteLine($"{(input ? "error" : "refused")}: {error.Message.ReplaceLineEndings(" ")} ({error.Code})");
        return input ? 1 : 3;
    }

    internal static int Usage(string problem, string usage)
    {
        Console.Error.WriteLine($"usage: {problem}");
        Console.Error.WriteLine(usage);
        return 2;
    }
}
