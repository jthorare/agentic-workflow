using Aw.Meta.Core.Commands;

namespace Aw.Meta.Infrastructure.Processes;

// How a request becomes a process: which program, which words, and which environment (the sandbox, and the PATH prefix #3441 adds).
internal sealed partial class SystemCommandRunner
{
    // A bare program name that sits in one of the prefix directories starts from there: the OS looks a name up in our PATH, not the child's.
    private static string? FromPrefix(string program, IReadOnlyList<string> prefix) =>
        program.Contains('/', StringComparison.Ordinal) ? null : prefix.Select(directory => Path.Combine(directory, program)).FirstOrDefault(File.Exists);

    // What to run and with which environment. An isolated command gets the sandbox in our process environment, or, behind a wrapper, through "env".
    private static (IReadOnlyList<string> Argv, IEnumerable<string> Unset, IEnumerable<KeyValuePair<string, string>> Env) Plan(CommandRequest request, Sandbox? sandbox)
    {
        if (sandbox is null)
        {
            return (request.Argv, request.UnsetEnv, request.Env);
        }

        return request.IsolatedFrom > 0 && request.IsolatedFrom < request.Argv.Count
            ? (WithEnv(request.Argv, request.IsolatedFrom, sandbox, request.PathPrefix), request.UnsetEnv, request.Env)
            : (request.Argv, request.UnsetEnv.Concat(Sandbox.Unset), sandbox.Variables().Concat(request.Env));
    }

    private static string WithPrefix(IReadOnlyList<string> prefix, string? path)
    {
        var front = string.Join(Path.PathSeparator, prefix);
        return string.IsNullOrEmpty(path) ? front : front + Path.PathSeparator + path;
    }

    // [wrapper..., env -u X... NAME=value..., command...]: the sandbox reaches the command and not the wrapper. env sets PATH last, so the
    // request's PATH prefix goes into that word too (#3441: a heavy-wrapped rerun lost node_modules/.bin).
    private static List<string> WithEnv(IReadOnlyList<string> argv, int from, Sandbox sandbox, IReadOnlyList<string> pathPrefix)
    {
        var words = new List<string>(argv.Take(from)) { "env" };
        foreach (var name in Sandbox.Unset)
        {
            words.Add("-u");
            words.Add(name);
        }

        var variables = sandbox.Variables();
        if (pathPrefix.Count > 0)
        {
            variables["PATH"] = WithPrefix(pathPrefix, variables.TryGetValue("PATH", out var path) ? path : Environment.GetEnvironmentVariable("PATH"));
        }

        words.AddRange(variables.Select(pair => pair.Key + "=" + pair.Value));
        words.AddRange(argv.Skip(from));
        return words;
    }
}
