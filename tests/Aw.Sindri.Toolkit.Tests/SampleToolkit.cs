namespace Aw.Sindri.Toolkit.Tests;

/// <summary>The toolkit the tests declare: a database, a disk, a connection and two tools.</summary>
internal static class SampleToolkit
{
    /// <summary>A line a failing tool puts in its exception, to prove it never reaches the client.</summary>
    public const string StackLine = "   at Secret.Namespace.Frame()";

    public static Toolkit Create() =>
        Toolkit.Named("contracts")
            .WithVersion("1.2.0")
            .UseJson(SampleJson.Default)
            .Needs(Resources.Database("cache"), Resources.Disk("scratch", "1GiB"), Resources.Connection("github"))
            .Tool<DiffRequest, DiffResponse>(
                "diff_contracts",
                "Compares two contracts and says what changed.",
                (request, _, _) => Task.FromResult(new DiffResponse($"{request.Left} vs {request.Right}", 1)))
            .Tool<DiffRequest, DiffResponse>(
                "explode",
                "Always fails, with a secret and a stack line in the exception.",
                (_, context, _) => throw new InvalidOperationException($"credential {context.Connection("github")} rejected{Environment.NewLine}{StackLine}"));
}
